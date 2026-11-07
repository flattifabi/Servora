namespace Servora.Application.Api;

/// <summary>KPIs of the API key administration page.</summary>
/// <param name="Active">The number of active keys.</param>
/// <param name="ExpiringSoon">The number of active keys expiring within 14 days.</param>
/// <param name="Revoked">The number of revoked keys.</param>
/// <param name="RequestsToday">Authenticated requests today (UTC).</param>
/// <param name="FailedToday">Rejected attempts today (UTC).</param>
/// <param name="Daily">Daily totals of the last 14 days, oldest first.</param>
public sealed record ApiKeyStats(int Active, int ExpiringSoon, int Revoked, long RequestsToday, long FailedToday, IReadOnlyList<ApiKeyUsagePoint> Daily);
