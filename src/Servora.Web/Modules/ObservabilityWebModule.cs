using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Servora.Application.Common;
using Servora.Infrastructure.Observability.Logging;
using Servora.Web.Observability;

namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-09 Observability &amp; Analytics module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ObservabilityWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddObservabilityWeb(this WebApplicationBuilder builder)
    {
        // Own logger provider: redacted ring buffer (log viewer) + rolling JSON-lines files in <data dir>/logs, and the
        // runtime debug switch that changes levels without restart.
        builder.Logging.AddServoraLogging(sp => sp.GetRequiredService<IAppPaths>().LogDirectory, builder.Configuration);

        // Correlation id + request metrics at the start of the pipeline (Program.cs stays untouched).
        builder.Services.AddTransient<IStartupFilter, ObservabilityStartupFilter>();

        // The existing /health endpoint (MapHealthChecks without explicit options) uses these options: JSON output,
        // minimal for anonymous callers, detailed for principals with admin.health.read.
        builder.Services.Configure<HealthCheckOptions>(options =>
        {
            options.ResponseWriter = HealthJsonResponseWriter.WriteAsync;
            options.AllowCachingResponses = false;
        });
        return builder;
    }
}
