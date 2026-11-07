namespace Servora.Application.Approvals;

/// <summary>Key figures of the current user's approval inbox.</summary>
/// <param name="Pending">Approvals waiting for my decision.</param>
/// <param name="ApprovedLast30Days">Approvals I approved in the last 30 days.</param>
/// <param name="RejectedLast30Days">Approvals I rejected in the last 30 days.</param>
/// <param name="OldestPendingAt">UTC request time of my oldest pending approval.</param>
public sealed record ApprovalInboxStats(int Pending, int ApprovedLast30Days, int RejectedLast30Days, DateTimeOffset? OldestPendingAt);
