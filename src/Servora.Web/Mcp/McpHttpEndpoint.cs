using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Infrastructure.Security;

namespace Servora.Web.Mcp;

/// <summary>
/// Streamable HTTP transport of the MCP server: JSON-RPC messages are POSTed to <c>/mcp</c> and answered with
/// <c>application/json</c> (the server never opens SSE streams, so GET/DELETE return 405). Validates <c>Origin</c>
/// (DNS rebinding protection), the <c>MCP-Protocol-Version</c> header, content type and body size.
/// </summary>
public static class McpHttpEndpoint
{
    /// <summary>Handles a POSTed JSON-RPC message or batch.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="server">The MCP server.</param>
    /// <param name="currentUser">The current actor (API key principal).</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> HandlePostAsync(HttpContext context, IMcpServer server, ICurrentUser currentUser)
    {
        DisableStatusCodePages(context);
        var request = context.Request;

        if (request.Headers.Origin is { Count: > 0 } origin
            && (!Uri.TryCreate(origin.ToString(), UriKind.Absolute, out var originUri)
                || !string.Equals(originUri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "Cross-origin MCP requests are not allowed.");
        }

        var version = request.Headers[McpProtocol.VersionHeader].ToString();
        if (!string.IsNullOrEmpty(version) && !McpProtocol.IsSupported(version))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unsupported MCP protocol version",
                detail: $"Supported versions: {string.Join(", ", McpProtocol.SupportedVersions)}.");
        }

        if (request.ContentType is null || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Unsupported media type", detail: "Send JSON-RPC messages as application/json.");
        }

        if (request.ContentLength > McpLimits.MaxRequestBytes)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Payload too large", detail: $"The maximum request size is {McpLimits.MaxRequestBytes / 1024} KB.");
        }

        var body = await ReadBoundedAsync(request, context.RequestAborted);
        if (body is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Payload too large", detail: $"The maximum request size is {McpLimits.MaxRequestBytes / 1024} KB.");
        }

        var scopes = context.User.FindAll(ServoraClaimTypes.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        var callContext = new McpCallContext(currentUser.ApiKeyId, scopes, currentUser.DisplayName, currentUser.IpAddress);
        var response = await server.HandleAsync(body, callContext, context.RequestAborted);
        return response is null
            ? Results.StatusCode(StatusCodes.Status202Accepted)
            : Results.Text(response, "application/json", Encoding.UTF8);
    }

    /// <summary>Answers GET / DELETE: this server offers no SSE stream and no sessions.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>405 with an <c>Allow</c> header.</returns>
    public static IResult MethodNotAllowed(HttpContext context)
    {
        DisableStatusCodePages(context);
        context.Response.Headers.Allow = "POST";
        return Results.Problem(statusCode: StatusCodes.Status405MethodNotAllowed, title: "Method not allowed", detail: "The Servora MCP endpoint accepts JSON-RPC messages via POST only.");
    }

    private static void DisableStatusCodePages(HttpContext context)
    {
        if (context.Features.Get<IStatusCodePagesFeature>() is { } feature)
        {
            feature.Enabled = false;
        }
    }

    private static async Task<string?> ReadBoundedAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 16 * 1024, leaveOpen: true);
        var buffer = new char[16 * 1024];
        var builder = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            builder.Append(buffer, 0, read);
            if (builder.Length > McpLimits.MaxRequestBytes)
            {
                return null;
            }
        }

        return builder.ToString();
    }
}
