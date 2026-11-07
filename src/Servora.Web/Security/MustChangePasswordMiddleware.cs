using Servora.Infrastructure.Security;

namespace Servora.Web.Security;

/// <summary>
/// Forces users flagged with <c>MustChangePassword</c> (for example the bootstrap <c>admin</c>) to the password change page
/// before any other page can be used. API calls receive HTTP 403.
/// </summary>
/// <param name="next">The next middleware.</param>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    /// <summary>The password change route.</summary>
    public const string ChangePasswordPath = "/account/change-password";

    private static readonly string[] s_allowedPrefixes = [ChangePasswordPath, "/auth/", "/_framework", "/_blazor", "/css", "/js", "/favicon", "/_content", "/health"];

    /// <summary>Processes the request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the pipeline is done.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim(ServoraClaimTypes.MustChangePassword, "true"))
        {
            var path = context.Request.Path.Value ?? "/";
            if (!s_allowedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(ChangePasswordPath);
                return Task.CompletedTask;
            }
        }

        return next(context);
    }
}
