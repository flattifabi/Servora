namespace Servora.Application.Observability;

/// <summary>HTTP request metrics over a time window (per-minute aggregation, at most 24 hours).</summary>
/// <param name="WindowMinutes">The window length in minutes.</param>
/// <param name="TotalRequests">Requests in the window.</param>
/// <param name="ClientErrors">4xx responses in the window.</param>
/// <param name="ServerErrors">5xx responses in the window.</param>
/// <param name="AverageMs">The average duration in the window.</param>
/// <param name="P95Ms">The approximate 95th percentile duration in the window.</param>
/// <param name="MaxMs">The maximum duration in the window.</param>
/// <param name="Minutes">One entry per minute of the window (oldest first, empty minutes included).</param>
/// <param name="TopRoutes">The busiest routes since start.</param>
public sealed record RequestMetricsSnapshot(
    int WindowMinutes,
    long TotalRequests,
    long ClientErrors,
    long ServerErrors,
    double AverageMs,
    double P95Ms,
    double MaxMs,
    IReadOnlyList<MetricsMinute> Minutes,
    IReadOnlyList<RouteMetrics> TopRoutes)
{
    /// <summary>Gets the share of 5xx responses in percent.</summary>
    public double ErrorRate => TotalRequests == 0 ? 0 : ServerErrors * 100.0 / TotalRequests;

    /// <summary>Gets the average number of requests per minute.</summary>
    public double RequestsPerMinute => WindowMinutes == 0 ? 0 : TotalRequests / (double)WindowMinutes;
}
