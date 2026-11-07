using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Servora.Infrastructure.Events;

namespace Servora.Infrastructure.Observability.Health;

/// <summary>
/// Reports the backlog of the record event queue (business rules, flows, SLAs, notifications). Degraded above 1,000
/// queued events, unhealthy above 8,000 (the queue holds 10,000 and then blocks writers).
/// </summary>
/// <param name="queue">The record event queue.</param>
public sealed class EventQueueHealthCheck(RecordEventQueue queue) : IHealthCheck
{
    /// <summary>Backlog above which the check reports Degraded.</summary>
    public const int DegradedBacklog = 1000;

    /// <summary>Backlog above which the check reports Unhealthy.</summary>
    public const int UnhealthyBacklog = 8000;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var backlog = queue.Count;
        var data = new Dictionary<string, object> { ["backlog"] = backlog.ToString(CultureInfo.InvariantCulture), ["capacity"] = "10000" };
        var text = $"{backlog} record event(s) queued.";
        return Task.FromResult(backlog > UnhealthyBacklog
            ? HealthCheckResult.Unhealthy("Record event processing is far behind: " + text, data: data)
            : backlog > DegradedBacklog
                ? HealthCheckResult.Degraded("Record event processing is behind: " + text, data: data)
                : HealthCheckResult.Healthy(text, data));
    }
}
