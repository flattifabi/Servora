using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Infrastructure.Observability.Health;

namespace Servora.Web.Observability;

/// <summary>
/// JSON response of <c>/health</c>. Anonymous callers (load balancers, monitoring) get the overall status only;
/// authenticated principals with <c>admin.health.read</c> get every check with its data, process facts and request
/// metrics. Never contains secrets or connection strings.
/// </summary>
public static class HealthJsonResponseWriter
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Writes the health report.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="report">The health report.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache";

        var detailed = context.User.Identity?.IsAuthenticated == true
            && await context.RequestServices.GetRequiredService<ICurrentUser>().HasPermissionAsync(Application.Security.Permissions.Observability.ReadHealth, context.RequestAborted);
        if (!detailed)
        {
            await JsonSerializer.SerializeAsync(context.Response.Body, new { status = report.Status.ToString(), checkedAt = DateTimeOffset.UtcNow }, s_json, context.RequestAborted);
            return;
        }

        var processMetrics = context.RequestServices.GetRequiredService<IProcessMetrics>();
        var requestMetrics = context.RequestServices.GetRequiredService<IRequestMetrics>().GetSnapshot(TimeSpan.FromHours(1));
        var sample = processMetrics.Current;
        var payload = new
        {
            status = report.Status.ToString(),
            checkedAt = DateTimeOffset.UtcNow,
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            version = SystemHealthService.ProductVersion,
            uptimeSeconds = (long)(DateTimeOffset.UtcNow - processMetrics.StartedAt).TotalSeconds,
            process = new
            {
                cpuPercent = sample.CpuPercent,
                workingSetBytes = sample.WorkingSetBytes,
                managedBytes = sample.ManagedBytes,
                threads = sample.ThreadCount,
            },
            requests = new
            {
                windowMinutes = requestMetrics.WindowMinutes,
                total = requestMetrics.TotalRequests,
                serverErrors = requestMetrics.ServerErrors,
                clientErrors = requestMetrics.ClientErrors,
                averageMs = Math.Round(requestMetrics.AverageMs, 1),
                p95Ms = requestMetrics.P95Ms,
            },
            checks = SystemHealthService.ToEntries(report).Select(e => new { name = e.Name, status = e.Status, description = e.Description, durationMs = e.DurationMs, data = e.Data }),
        };
        await JsonSerializer.SerializeAsync(context.Response.Body, payload, s_json, context.RequestAborted);
    }
}
