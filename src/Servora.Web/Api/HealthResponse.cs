namespace Servora.Web.Api;

/// <summary>Response of <c>GET /api/v1/health</c> (anonymous, no sensitive details).</summary>
/// <param name="Status">The status: <c>healthy</c>, <c>degraded</c> or <c>setup</c>.</param>
/// <param name="Version">The product version.</param>
/// <param name="Database">The database state: <c>reachable</c>, <c>unreachable</c> or <c>not_configured</c>.</param>
/// <param name="Timestamp">The server time (UTC).</param>
public sealed record HealthResponse(string Status, string Version, string Database, DateTimeOffset Timestamp);
