namespace Servora.Application.Observability;

/// <summary>Audit event counts of one UTC day.</summary>
/// <param name="Day">The UTC date.</param>
/// <param name="Success">Successful events.</param>
/// <param name="Failure">Failed events.</param>
/// <param name="Denied">Denied events.</param>
public sealed record AuditDayCount(DateOnly Day, int Success, int Failure, int Denied);
