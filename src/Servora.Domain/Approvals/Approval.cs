using Servora.Domain.Common;

namespace Servora.Domain.Approvals;

/// <summary>
/// One approver's approval of a record of any table (change request, requested item, flow step, ...). All rows created
/// by one approval request share the same <see cref="BatchId"/> ("approval round"); the round's outcome is derived from
/// the row states and the <see cref="Mode"/>.
/// </summary>
[RecordTable(TableNames.Approval, "Approval", PluralLabel = "Approvals", DisplayField = nameof(RecordNumber), Icon = "check-square", Route = "/approvals")]
public class Approval : AuditableEntity
{
    /// <summary>Gets or sets the technical table name of the approved record.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the identifier of the approved record.</summary>
    public Guid RecordId { get; set; }

    /// <summary>Gets or sets the display number of the approved record (denormalized for fast inbox rendering).</summary>
    public string? RecordNumber { get; set; }

    /// <summary>Gets or sets the one-line summary of the approved record.</summary>
    public string? RecordSummary { get; set; }

    /// <summary>Gets or sets the relative UI route of the approved record.</summary>
    public string? RecordRoute { get; set; }

    /// <summary>Gets or sets the identifier shared by all rows of one approval round.</summary>
    public Guid BatchId { get; set; }

    /// <summary>Gets or sets the approver.</summary>
    [Reference(TableNames.User)]
    public Guid ApproverId { get; set; }

    /// <summary>Gets or sets the approval group through which the approver was added, if any.</summary>
    [Reference(TableNames.Group)]
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets how the decisions of the round are combined.</summary>
    public ApprovalMode Mode { get; set; } = ApprovalMode.AnyApprover;

    /// <summary>Gets or sets the decision state.</summary>
    public ApprovalRecordState State { get; set; } = ApprovalRecordState.Requested;

    /// <summary>Gets or sets the text shown to the approver explaining what is approved.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets the approver's comment.</summary>
    public string? Comments { get; set; }

    /// <summary>Gets or sets the user that requested the approval.</summary>
    [Reference(TableNames.User)]
    public Guid? RequestedById { get; set; }

    /// <summary>Gets or sets the UTC time the approval was requested.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Gets or sets the UTC time of the decision.</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>Gets or sets the user that recorded the decision (the approver or an approval administrator on their behalf).</summary>
    [Reference(TableNames.User)]
    public Guid? DecidedById { get; set; }
}
