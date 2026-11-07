namespace Servora.Application.Observability;

/// <summary>Request totals of one route template since start.</summary>
/// <param name="Route">The route template (for example <c>/api/v1/table/{table}</c>).</param>
/// <param name="Count">The number of requests.</param>
/// <param name="Errors">Responses with status 5xx.</param>
/// <param name="AverageMs">The average duration in milliseconds.</param>
/// <param name="MaxMs">The maximum duration in milliseconds.</param>
public sealed record RouteMetrics(string Route, long Count, long Errors, double AverageMs, double MaxMs);
