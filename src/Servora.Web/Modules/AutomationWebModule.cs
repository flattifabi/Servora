using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-08 Automation &amp; Integration module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class AutomationWebModule
{
    /// <summary>The rate limiting policy of the inbound webhook endpoint (60 calls per minute and client IP).</summary>
    public const string WebhookRateLimitPolicy = "automation-webhook";

    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddAutomationWeb(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<RateLimiterOptions>(options => options.AddPolicy(WebhookRateLimitPolicy, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return builder;
    }
}
