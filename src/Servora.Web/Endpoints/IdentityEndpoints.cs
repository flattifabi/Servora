using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Servora.Application.Identity.Windows;
using Servora.Web.Security;

namespace Servora.Web.Endpoints;

/// <summary>HTTP endpoints of the T-02 Identity &amp; Access module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class IdentityEndpoints
{
    /// <summary>The Windows integrated sign-in route.</summary>
    public const string WindowsSignInPath = "/auth/windows";

    private const string ChallengeFallbackHtml = """
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>Windows sign-in · Servora</title>
        <style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#0b0d17;color:#e6e8f2;font:15px/1.55 system-ui,-apple-system,Segoe UI,sans-serif}
        main{max-width:440px;padding:32px;border:1px solid #262a3d;border-radius:16px;background:#12152a}
        h1{font-size:20px;margin:0 0 8px}p{color:#a3a8c3;margin:0 0 20px}
        a{display:inline-block;padding:10px 16px;border-radius:10px;background:#7c6cff;color:#fff;text-decoration:none;font-weight:600}</style></head>
        <body><main><h1>Windows sign-in was not completed</h1>
        <p>Your browser did not provide Windows credentials. Make sure you are signed in to the domain and that this site is
        in your browser's intranet zone, or sign in with your user name and password.</p>
        <a href="/login">Back to sign-in</a></main></body></html>
        """;

    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(WindowsSignInPath, WindowsSignInAsync)
            .AllowAnonymous()
            .WithTags("Authentication")
            .ExcludeFromDescription();
        return app;
    }

    /// <summary>
    /// Windows integrated sign-in: challenges the Negotiate scheme (only when enabled), maps the Windows identity to a
    /// Servora user and issues the normal application cookie.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="returnUrl">The local URL to return to.</param>
    /// <param name="windows">The Windows sign-in service.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A redirect, or a 401 Negotiate challenge.</returns>
    private static async Task<IResult> WindowsSignInAsync(HttpContext context, [FromQuery] string? returnUrl, IWindowsSignInService windows, CancellationToken cancellationToken)
    {
        var safeReturnUrl = AuthEndpoints.SafeReturnUrl(returnUrl);
        if (!await windows.IsEnabledAsync(cancellationToken))
        {
            return Results.Redirect("/login?error=" + Uri.EscapeDataString("Windows sign-in is not enabled."));
        }

        var result = await context.AuthenticateAsync(NegotiateDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal?.Identity is not { IsAuthenticated: true, Name: { Length: > 0 } windowsName })
        {
            // 401 + WWW-Authenticate: Negotiate. Browsers with domain credentials retry automatically; others show the
            // fallback page (status code pages are disabled so the challenge header is preserved).
            if (context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
            {
                statusCodePages.Enabled = false;
            }

            await context.ChallengeAsync(NegotiateDefaults.AuthenticationScheme);
            if (!context.Response.HasStarted)
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(ChallengeFallbackHtml, cancellationToken);
            }

            return Results.Empty;
        }

        var sid = result.Principal.FindFirstValue(ClaimTypes.PrimarySid);
        var signIn = await windows.SignInAsync(windowsName, sid, cancellationToken);
        if (!signIn.Succeeded)
        {
            return Results.Redirect($"/login?error={Uri.EscapeDataString(signIn.Message)}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
        }

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            UserPrincipalFactory.Create(signIn.User!, signIn.AuthSource),
            new AuthenticationProperties { IsPersistent = false });
        return Results.Redirect(safeReturnUrl);
    }
}
