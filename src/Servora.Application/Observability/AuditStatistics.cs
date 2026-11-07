namespace Servora.Application.Observability;

/// <summary>KPIs of the audit log viewer.</summary>
/// <param name="Total">Total number of retained events.</param>
/// <param name="Last24Hours">Events in the last 24 hours.</param>
/// <param name="Failures24Hours">Failed events in the last 24 hours.</param>
/// <param name="Denied24Hours">Denied events in the last 24 hours.</param>
/// <param name="ByCategory">Event counts per category in the statistics period.</param>
/// <param name="PerDay">Event counts per day in the statistics period (oldest first).</param>
/// <param name="RetentionDays">The audit retention in days (0 = keep forever).</param>
public sealed record AuditStatistics(
    int Total,
    int Last24Hours,
    int Failures24Hours,
    int Denied24Hours,
    IReadOnlyList<NamedCount> ByCategory,
    IReadOnlyList<AuditDayCount> PerDay,
    int RetentionDays);
