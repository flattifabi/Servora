namespace Servora.Application.Observability;

/// <summary>Complete health picture shown on <c>/admin/health</c> and in the detailed <c>/health</c> response.</summary>
/// <param name="Status">The overall status: <c>Healthy</c>, <c>Degraded</c> or <c>Unhealthy</c>.</param>
/// <param name="CheckedAt">The UTC time of the check.</param>
/// <param name="DurationMs">The total duration of all checks.</param>
/// <param name="Checks">The individual checks.</param>
/// <param name="Process">Process facts.</param>
/// <param name="ProcessHistory">Recent resource samples (oldest first).</param>
/// <param name="Requests">HTTP request metrics of the last hour.</param>
/// <param name="Heartbeats">Background service heartbeats.</param>
public sealed record SystemHealthReport(
    string Status,
    DateTimeOffset CheckedAt,
    double DurationMs,
    IReadOnlyList<HealthCheckEntry> Checks,
    ProcessInfo Process,
    IReadOnlyList<ProcessSample> ProcessHistory,
    RequestMetricsSnapshot Requests,
    IReadOnlyList<HeartbeatInfo> Heartbeats);
