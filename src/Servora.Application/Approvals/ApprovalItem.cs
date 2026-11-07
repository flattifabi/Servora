using Servora.Domain.Approvals;

namespace Servora.Application.Approvals;

/// <summary>An approval as shown in the approval inbox and on record pages.</summary>
public sealed record ApprovalItem
{
    /// <summary>Gets the approval identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the technical table name of the approved record.</summary>
    public string TableName { get; init; } = string.Empty;

    /// <summary>Gets the display label of the table.</summary>
    public string TableLabel { get; init; } = string.Empty;

    /// <summary>Gets the record identifier.</summary>
    public Guid RecordId { get; init; }

    /// <summary>Gets the record number.</summary>
    public string? RecordNumber { get; init; }

    /// <summary>Gets the record summary.</summary>
    public string? RecordSummary { get; init; }

    /// <summary>Gets the relative UI route of the record.</summary>
    public string? RecordRoute { get; init; }

    /// <summary>Gets the text shown to the approver.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the decision state.</summary>
    public ApprovalRecordState State { get; init; }

    /// <summary>Gets the policy of the round.</summary>
    public ApprovalMode Mode { get; init; }

    /// <summary>Gets the approver identifier.</summary>
    public Guid ApproverId { get; init; }

    /// <summary>Gets the approver display name.</summary>
    public string ApproverName { get; init; } = string.Empty;

    /// <summary>Gets the name of the approval group through which the approver was added.</summary>
    public string? GroupName { get; init; }

    /// <summary>Gets the display name of the requester.</summary>
    public string? RequestedByName { get; init; }

    /// <summary>Gets the UTC request time.</summary>
    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>Gets the UTC decision time.</summary>
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Gets the approver comment.</summary>
    public string? Comments { get; init; }

    /// <summary>Gets a value indicating whether the current user is the approver.</summary>
    public bool IsMine { get; init; }

    /// <summary>Gets a value indicating whether the current user can decide the approval now.</summary>
    public bool CanDecide { get; init; }
}
