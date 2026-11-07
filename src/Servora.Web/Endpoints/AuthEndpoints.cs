using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Identity;
using Servora.Application.Observability;
using Servora.Domain.Identity;
using Servora.Web.Security;

namespace Servora.Web.Endpoints;

/// <summary>Form endpoints for interactive sign-in, sign-out and the forced password change.</summary>
public static class AuthEndpoints
{
    /// <summary>The rate limiter policy applied to credential endpoints.</summary>
    public const string RateLimitPolicy = "auth";

    /// <summary>Maps the endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Authentication").ExcludeFromDescription();

        group.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicy);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization().RequireRateLimiting(RateLimitPolicy);
        return app;
    }

    /// <summary>Returns <paramref name="returnUrl"/> when it is a safe local URL, otherwise <c>/</c>.</summary>
    /// <param name="returnUrl">The requested return URL.</param>
    /// <returns>A safe local URL.</returns>
    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        [FromForm] string? userName,
        [FromForm] string? password,
        [FromForm] string? returnUrl,
        [FromForm] bool? rememberMe,
        ISignInService signIn,
        CancellationToken cancellationToken)
    {
        var result = await signIn.ValidateCredentialsAsync(userName ?? string.Empty, password ?? string.Empty, cancellationToken);
        if (!result.Succeeded)
        {
            var query = $"?error={Uri.EscapeDataString(result.Message)}&userName={Uri.EscapeDataString(userName ?? string.Empty)}";
            if (!string.IsNullOrEmpty(returnUrl))
            {
                query += $"&returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}";
            }

            return Results.Redirect("/login" + query);
        }

        var principal = UserPrincipalFactory.Create(result.User!, result.AuthSource);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = rememberMe == true,
            ExpiresUtc = rememberMe == true ? DateTimeOffset.UtcNow.AddDays(14) : null,
        });

        return Results.Redirect(result.User!.MustChangePassword && result.User.Source == UserSource.Local
            ? MustChangePasswordMiddleware.ChangePasswordPath
            : SafeReturnUrl(returnUrl));
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAuditService audit, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry("auth", "logout"), cancellationToken);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login?signedOut=1");
    }

    private static async Task<IResult> ChangePasswordAsync(
        HttpContext context,
        [FromForm] string? currentPassword,
        [FromForm] string? newPassword,
        [FromForm] string? confirmPassword,
        ISignInService signIn,
        IAppDbContextFactory dbFactory,
        SecurityStampValidator stampValidator,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Redirect("/login");
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return Results.Redirect($"{MustChangePasswordMiddleware.ChangePasswordPath}?error={Uri.EscapeDataString("The new passwords do not match.")}");
        }

        var result = await signIn.ChangePasswordAsync(userId, currentPassword ?? string.Empty, newPassword ?? string.Empty, cancellationToken);
        if (!result.Succeeded)
        {
            return Results.Redirect($"{MustChangePasswordMiddleware.ChangePasswordPath}?error={Uri.EscapeDataString(result.Error ?? "The password could not be changed.")}");
        }

        stampValidator.Invalidate(userId);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);
        var source = context.User.FindFirstValue(Infrastructure.Security.ServoraClaimTypes.AuthSource) ?? "local";
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserPrincipalFactory.Create(user, source));
        return Results.Redirect("/?passwordChanged=1");
    }
}
