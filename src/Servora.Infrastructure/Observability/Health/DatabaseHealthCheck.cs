using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Servora.Application.Common;
using Servora.Domain.Configuration;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Observability.Health;

/// <summary>
/// Checks database connectivity and round-trip latency with a tiny bounded query. Degraded above
/// <see cref="DegradedLatencyMs"/>, unhealthy when the database cannot be reached. Never reports connection strings.
/// </summary>
/// <param name="settings">The database settings store.</param>
/// <param name="dbFactory">The database context factory.</param>
public sealed class DatabaseHealthCheck(DatabaseSettingsStore settings, IAppDbContextFactory dbFactory) : IHealthCheck
{
    /// <summary>The latency above which the check reports Degraded.</summary>
    public const double DegradedLatencyMs = 1000;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (settings.Current is not { } current)
        {
            return HealthCheckResult.Degraded("No database is configured yet (setup wizard pending).", data: new Dictionary<string, object> { ["provider"] = "none" });
        }

        var data = new Dictionary<string, object> { ["provider"] = current.Provider };
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var stopwatch = Stopwatch.StartNew();
            _ = await db.Set<SystemSetting>().AsNoTracking().Select(s => s.Id).FirstOrDefaultAsync(cancellationToken);
            stopwatch.Stop();
            var latency = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1);
            data["latencyMs"] = latency.ToString(CultureInfo.InvariantCulture);
            return latency > DegradedLatencyMs
                ? HealthCheckResult.Degraded($"Database reachable but slow ({latency:0} ms).", data: data)
                : HealthCheckResult.Healthy($"{current.Provider} reachable ({latency:0.#} ms).", data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Only the exception type is reported: provider messages can contain server names.
            data["error"] = ex.GetType().Name;
            return HealthCheckResult.Unhealthy("The database cannot be reached.", data: data);
        }
    }
}
