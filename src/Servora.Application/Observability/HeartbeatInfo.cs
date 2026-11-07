namespace Servora.Application.Observability;

/// <summary>Last heartbeat of a background service.</summary>
/// <param name="Name">The service name.</param>
/// <param name="LastBeat">The UTC time of the last heartbeat.</param>
/// <param name="ExpectedInterval">The interval in which the service promises to beat.</param>
/// <param name="Status">An optional short status text (for example "idle" or "processed 12 events").</param>
public sealed record HeartbeatInfo(string Name, DateTimeOffset LastBeat, TimeSpan ExpectedInterval, string? Status)
{
    /// <summary>Checks whether the heartbeat is overdue (more than three expected intervals, at least one minute).</summary>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when stale.</returns>
    public bool IsStale(DateTimeOffset now) => now - LastBeat > TimeSpan.FromTicks(Math.Max(ExpectedInterval.Ticks * 3, TimeSpan.FromMinutes(1).Ticks));
}
