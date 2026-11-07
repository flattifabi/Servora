namespace Servora.Application.Observability;

/// <summary>Aggregated HTTP request metrics of one minute.</summary>
/// <param name="Minute">The start of the minute (UTC).</param>
/// <param name="Count">The number of requests.</param>
/// <param name="ClientErrors">Responses with status 4xx.</param>
/// <param name="ServerErrors">Responses with status 5xx.</param>
/// <param name="AverageMs">The average duration in milliseconds.</param>
/// <param name="P95Ms">The approximate 95th percentile duration in milliseconds.</param>
/// <param name="MaxMs">The maximum duration in milliseconds.</param>
public sealed record MetricsMinute(DateTimeOffset Minute, int Count, int ClientErrors, int ServerErrors, double AverageMs, double P95Ms, double MaxMs);
