namespace Servora.Application.Approvals;

/// <summary>Description of an approvable record shown to approvers.</summary>
/// <param name="Number">The display number, for example <c>CHG0001001</c>.</param>
/// <param name="Summary">The one-line summary.</param>
/// <param name="Route">The relative UI route of the record.</param>
public sealed record ApprovalRecordInfo(string Number, string Summary, string? Route);
