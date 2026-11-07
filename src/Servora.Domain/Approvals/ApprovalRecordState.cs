namespace Servora.Domain.Approvals;

/// <summary>State of one approver's decision (<see cref="Approval"/> row).</summary>
public enum ApprovalRecordState
{
    /// <summary>The approver has not decided yet.</summary>
    Requested = 0,

    /// <summary>The approver approved.</summary>
    Approved = 1,

    /// <summary>The approver rejected.</summary>
    Rejected = 2,

    /// <summary>The approval was withdrawn (for example because the record was cancelled or re-submitted).</summary>
    Cancelled = 3,

    /// <summary>The decision is no longer needed because the round was already decided by other approvers.</summary>
    NotRequired = 4,
}
