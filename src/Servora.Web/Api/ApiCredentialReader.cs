using Servora.Application.Api;

namespace Servora.Web.Api;

/// <summary>Reads API key tokens from requests (<c>X-Api-Key</c> header or <c>Authorization: Bearer srv_…</c>).</summary>
public static class ApiCredentialReader
{
    /// <summary>Reads the presented token.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The token, or <see langword="null"/> when no API key is presented.</returns>
    public static string? ReadToken(HttpRequest request)
    {
        if (request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var header) && header.Count == 1 && !string.IsNullOrWhiteSpace(header[0]))
        {
            return header[0]!.Trim();
        }

        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization["Bearer ".Length..].Trim();
            if (token.StartsWith(ApiKeyTokens.Prefix, StringComparison.Ordinal))
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>Checks whether the request is an API surface request (<c>/api</c>, <c>/mcp</c>, <c>/openapi</c>).</summary>
    /// <param name="path">The request path.</param>
    /// <returns><see langword="true"/> for API paths.</returns>
    public static bool IsApiPath(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/mcp") || path.StartsWithSegments("/openapi");
}
