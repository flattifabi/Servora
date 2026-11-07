using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Health;

/// <summary>Reports CPU, memory, threads and uptime of the process. Degraded when CPU usage stays above 90 %.</summary>
/// <param name="metrics">The process metrics.</param>
public sealed class ProcessHealthCheck(IProcessMetrics metrics) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var sample = metrics.Current;
        var recent = metrics.History.TakeLast(4).ToList();
        var uptime = DateTimeOffset.UtcNow - metrics.StartedAt;
        var data = new Dictionary<string, object>
        {
            ["cpuPercent"] = sample.CpuPercent.ToString("0.0", CultureInfo.InvariantCulture),
            ["workingSetMb"] = (sample.WorkingSetBytes / 1024d / 1024d).ToString("0", CultureInfo.InvariantCulture),
            ["managedMb"] = (sample.ManagedBytes / 1024d / 1024d).ToString("0", CultureInfo.InvariantCulture),
            ["threads"] = sample.ThreadCount.ToString(CultureInfo.InvariantCulture),
            ["uptimeSeconds"] = ((long)uptime.TotalSeconds).ToString(CultureInfo.InvariantCulture),
        };
        var text = string.Create(CultureInfo.InvariantCulture, $"CPU {sample.CpuPercent:0.#} %, {sample.WorkingSetBytes / 1024d / 1024d:0} MB working set, {sample.ThreadCount} threads.");
        var sustainedHigh = recent.Count >= 3 && recent.All(s => s.CpuPercent > 90);
        return Task.FromResult(sustainedHigh ? HealthCheckResult.Degraded("Sustained high CPU usage: " + text, data: data) : HealthCheckResult.Healthy(text, data));
    }
}
