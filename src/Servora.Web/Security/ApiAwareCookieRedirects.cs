using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Servora.Web.Api;

namespace Servora.Web.Security;

/// <summary>
/// Redirect handlers of the cookie scheme: UI requests are redirected to the sign-in / access denied page, API requests
/// (<c>/api</c>, <c>/mcp</c>, <c>/openapi</c> – also browser calls carrying the cookie) receive a 401 / 403 problem
/// response instead of an HTML redirect.
/// </summary>
public static class ApiAwareCookieRedirects
{
    /// <summary>Handles <see cref="CookieAuthenticationEvents.OnRedirectToLogin"/>.</summary>
    /// <param name="context">The redirect context.</param>
    /// <returns>A task that completes when the response was prepared.</returns>
    public static Task RedirectToLoginAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ApiProblemWriter.IsApiRequest(context.HttpContext))
        {
            return ApiProblemWriter.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, detail: "Your session is missing or has expired. Sign in again or send an API key.");
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    /// <summary>Handles <see cref="CookieAuthenticationEvents.OnRedirectToAccessDenied"/>.</summary>
    /// <param name="context">The redirect context.</param>
    /// <returns>A task that completes when the response was prepared.</returns>
    public static Task RedirectToAccessDeniedAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ApiProblemWriter.IsApiRequest(context.HttpContext))
        {
            return ApiProblemWriter.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, detail: "You lack the permission required for this operation.");
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
