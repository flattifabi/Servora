using Microsoft.AspNetCore.Authentication.Cookies;
using Servora.Web.Security;

namespace Servora.Web.Api;

/// <summary>
/// Forward selector of the default policy scheme: API keys authenticate only API paths (<c>/api</c>, <c>/openapi</c>)
/// and the MCP endpoint; everything else (Blazor UI) uses the cookie. <c>/mcp</c> never accepts cookies.
/// </summary>
public static class ApiSchemeSelector
{
    /// <summary>Selects the authentication scheme for a request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The scheme name.</returns>
    public static string Select(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/mcp"))
        {
            return ApiKeyAuthenticationDefaults.Scheme;
        }

        if (!ApiCredentialReader.IsApiPath(path))
        {
            return CookieAuthenticationDefaults.AuthenticationScheme;
        }

        if (ApiCredentialReader.ReadToken(context.Request) is not null)
        {
            return ApiKeyAuthenticationDefaults.Scheme;
        }

        // Browser calls (API Explorer "try it") carry the auth cookie; other callers get an API key challenge.
        var cookieName = context.RequestServices?.GetService<InstanceCookieNames>()?.Authentication ?? ApiKeyAuthenticationDefaults.AuthCookieName;
        return context.Request.Cookies.ContainsKey(cookieName)
            ? CookieAuthenticationDefaults.AuthenticationScheme
            : ApiKeyAuthenticationDefaults.Scheme;
    }
}
