using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Servora.Application.Identity;
using Servora.Application.Identity.Policies;
using Servora.Web.Security;

namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-02 Identity &amp; Access module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class IdentityWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddIdentityWeb(this WebApplicationBuilder builder)
    {
        // Windows integrated authentication (Kerberos / NTLM). The scheme is registered but never the default: it is only
        // challenged by /auth/windows, and only while Windows sign-in is enabled on /admin/directory. Without a domain
        // (or on platforms without Kerberos) the challenge simply fails and users sign in with a password.
        builder.Services.AddAuthentication().AddNegotiate();

        // Drop cached cookie / circuit validation state immediately when an identity service rotates a security stamp.
        builder.Services.AddScoped<IUserSessionInvalidator>(sp =>
        {
            var validator = sp.GetRequiredService<SecurityStampValidator>();
            return new DelegateUserSessionInvalidator(validator.Invalidate);
        });

        // Session lifetime from the authentication policy (/admin/auth): applied when the cookie is issued. Sliding
        // expiration keeps the same lifetime on renewal.
        builder.Services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnSigningIn;
            options.Events.OnSigningIn = async context =>
            {
                await previous(context);
                var policy = await context.HttpContext.RequestServices.GetRequiredService<IAuthPolicyProvider>().GetAsync(context.HttpContext.RequestAborted);
                var issued = context.Properties.IssuedUtc ?? DateTimeOffset.UtcNow;
                context.Properties.ExpiresUtc = context.Properties.IsPersistent
                    ? issued.AddDays(policy.RememberMeDays)
                    : issued.AddMinutes(policy.SessionLifetimeMinutes);
            };
        });

        return builder;
    }
}
