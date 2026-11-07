namespace Servora.Application.Approvals;

/// <summary>Request to approve a record (change, requested item, flow step, ...).</summary>
/// <param name="TableName">The technical table name of the record.</param>
/// <param name="RecordId">The record identifier.</param>
/// <param name="ApproverUserIds">Individual approvers.</param>
/// <param name="ApproverGroupIds">Approval groups (every active member becomes an approver).</param>
/// <param name="Policy">How decisions are combined.</param>
/// <param name="Reason">Optional text shown to approvers.</param>
public sealed record ApprovalRequest(
    string TableName,
    Guid RecordId,
    IReadOnlyList<Guid> ApproverUserIds,
    IReadOnlyList<Guid> ApproverGroupIds,
    ApprovalPolicy Policy = ApprovalPolicy.AnyApprover,
    string? Reason = null);
