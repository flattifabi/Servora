using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Servora.Web.Api;

/// <summary>
/// Writes RFC 7807 problem responses for the API surface (<c>/api</c>, <c>/mcp</c>, <c>/openapi</c>). API callers never
/// receive HTML (sign-in redirects, the "not found" page or the error page); every error status carries an
/// <c>application/problem+json</c> body instead – regardless of the <c>Accept</c> header.
/// </summary>
public static class ApiProblemWriter
{
    /// <summary>The media type of problem responses.</summary>
    public const string ProblemContentType = "application/problem+json";

    /// <summary>Checks whether the request targets the API surface.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns><see langword="true"/> for API requests.</returns>
    public static bool IsApiRequest(HttpContext context) => ApiCredentialReader.IsApiPath(context.Request.Path);

    /// <summary>Gets the default title and detail of an error status code.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The title and detail.</returns>
    public static (string Title, string Detail) Describe(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ("Bad request", "The request could not be understood."),
        StatusCodes.Status401Unauthorized => ("Unauthorized", $"Authentication is required. Send an API key in the '{ApiKeyAuthenticationDefaults.HeaderName}' header or as 'Authorization: Bearer srv_…'."),
        StatusCodes.Status403Forbidden => ("Forbidden", "You are not allowed to perform this operation."),
        StatusCodes.Status404NotFound => ("Not found", "The requested resource does not exist."),
        StatusCodes.Status405MethodNotAllowed => ("Method not allowed", "The HTTP method is not supported by this resource."),
        StatusCodes.Status415UnsupportedMediaType => ("Unsupported media type", "Send the request body as application/json."),
        StatusCodes.Status429TooManyRequests => ("Too many requests", "The rate limit was exceeded. Retry later."),
        StatusCodes.Status503ServiceUnavailable => ("Service unavailable", "The service is not available yet (for example while the setup wizard has not completed)."),
        >= 500 => ("Server error", "An unexpected error occurred. Quote the correlation id when contacting your administrator."),
        _ => ("Request failed", $"The request failed with status {statusCode}."),
    };

    /// <summary>Writes a problem response (no-op when the response has already started).</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="title">The problem title; defaults to <see cref="Describe"/>.</param>
    /// <param name="detail">The problem detail; defaults to <see cref="Describe"/>.</param>
    /// <returns>A task that completes when the response was written.</returns>
    public static async Task WriteAsync(HttpContext context, int statusCode, string? title = null, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Response.HasStarted)
        {
            return;
        }

        var defaults = Describe(statusCode);
        context.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? defaults.Title,
            Detail = detail ?? defaults.Detail,
            Type = $"https://httpstatuses.io/{statusCode}",
        };

        // The problem details service adds the correlation id; it declines for "Accept: text/html", so fall back to JSON.
        var service = context.RequestServices?.GetService<IProblemDetailsService>();
        if (service is null || !await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem }))
        {
            problem.Extensions["correlationId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, ProblemContentType);
        }
    }

    /// <summary>Status code page handler of the API branch: adds a problem body to empty error responses.</summary>
    /// <param name="context">The status code context.</param>
    /// <returns>A task that completes when the response was written.</returns>
    public static Task WriteStatusCodeAsync(StatusCodeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode);
    }
}
