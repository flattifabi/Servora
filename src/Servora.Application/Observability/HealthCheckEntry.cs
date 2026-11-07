namespace Servora.Application.Observability;

/// <summary>Result of one health check.</summary>
/// <param name="Name">The check name (for example <c>database</c>).</param>
/// <param name="Status">The status: <c>Healthy</c>, <c>Degraded</c> or <c>Unhealthy</c>.</param>
/// <param name="Description">A human readable, secret-free description.</param>
/// <param name="DurationMs">The check duration in milliseconds.</param>
/// <param name="Data">Additional values (latency, free space, backlog, …) as invariant strings.</param>
public sealed record HealthCheckEntry(string Name, string Status, string? Description, double DurationMs, IReadOnlyDictionary<string, string> Data);
