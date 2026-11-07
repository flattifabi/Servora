namespace Servora.Application.Approvals;

/// <summary>Approval status of a record.</summary>
/// <param name="State">The aggregated state.</param>
/// <param name="Pending">Number of pending approver decisions.</param>
/// <param name="Approved">Number of approvals.</param>
/// <param name="Rejected">Number of rejections.</param>
public sealed record ApprovalSummary(ApprovalState State, int Pending, int Approved, int Rejected);
