namespace Servora.Application.Observability;

/// <summary>One integrity problem found by the audit hash-chain verification.</summary>
/// <param name="Kind">The kind of problem.</param>
/// <param name="EventId">The affected event.</param>
/// <param name="Sequence">The chain position of the affected event.</param>
/// <param name="OccurredAt">The event time.</param>
/// <param name="Message">A human readable explanation.</param>
public sealed record AuditChainIssue(AuditChainIssueKind Kind, Guid EventId, long Sequence, DateTimeOffset OccurredAt, string Message);
