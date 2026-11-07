using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Servora.Application.Common;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Health;

/// <summary>
/// Checks that every <see cref="BackgroundService"/> is still running (not faulted or stopped) and that no registered
/// heartbeat is overdue. Unhealthy when a background service crashed, degraded when a heartbeat is stale.
/// </summary>
/// <param name="hostedServices">The registered hosted services.</param>
/// <param name="heartbeats">The heartbeat registry.</param>
/// <param name="clock">The clock.</param>
public sealed class BackgroundServicesHealthCheck(IEnumerable<IHostedService> hostedServices, IHeartbeatRegistry heartbeats, IClock clock) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var faulted = new List<string>();
        var stopped = new List<string>();
        foreach (var service in hostedServices.OfType<BackgroundService>())
        {
            var name = service.GetType().Name;
            var state = service.ExecuteTask switch
            {
                null => "starting",
                { IsFaulted: true } => "faulted",
                { IsCompleted: true } => "stopped",
                _ => "running",
            };
            data[name] = state;
            if (state == "faulted")
            {
                faulted.Add(name);
            }
            else if (state == "stopped")
            {
                stopped.Add(name);
            }
        }

        var now = clock.UtcNow;
        var stale = new List<string>();
        foreach (var beat in heartbeats.GetAll())
        {
            var isStale = beat.IsStale(now);
            data["heartbeat:" + beat.Name] = isStale ? $"stale since {beat.LastBeat:u}" : $"ok ({(int)(now - beat.LastBeat).TotalSeconds}s ago)";
            if (isStale)
            {
                stale.Add(beat.Name);
            }
        }

        if (faulted.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Background service(s) crashed: {string.Join(", ", faulted)}.", data: data));
        }

        if (stale.Count > 0 || stopped.Count > 0)
        {
            var parts = new List<string>();
            if (stale.Count > 0)
            {
                parts.Add($"heartbeat overdue: {string.Join(", ", stale)}");
            }

            if (stopped.Count > 0)
            {
                parts.Add($"stopped: {string.Join(", ", stopped)}");
            }

            return Task.FromResult(HealthCheckResult.Degraded(string.Join("; ", parts) + ".", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"{data.Count(d => !d.Key.StartsWith("heartbeat:", StringComparison.Ordinal))} background service(s) running.", data));
    }
}
