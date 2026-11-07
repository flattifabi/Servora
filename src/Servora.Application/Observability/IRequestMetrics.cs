namespace Servora.Application.Observability;

/// <summary>In-memory HTTP request metrics (count, latency, errors) aggregated per minute for the last 24 hours (singleton).</summary>
public interface IRequestMetrics
{
    /// <summary>Records a completed request.</summary>
    /// <param name="route">The route template or a coarse category (never raw paths with ids).</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="elapsedMs">The duration in milliseconds.</param>
    void Record(string route, int statusCode, double elapsedMs);

    /// <summary>Gets a snapshot of the most recent window.</summary>
    /// <param name="window">The window (1 minute .. 24 hours).</param>
    /// <returns>The snapshot.</returns>
    RequestMetricsSnapshot GetSnapshot(TimeSpan window);
}
