namespace Servora.Domain.Approvals;

/// <summary>Aggregated approval status stored on approvable records (changes, requested items).</summary>
public enum ApprovalStatus
{
    /// <summary>No approval was requested.</summary>
    NotRequested = 0,

    /// <summary>Approval is pending.</summary>
    Requested = 1,

    /// <summary>Approved.</summary>
    Approved = 2,

    /// <summary>Rejected.</summary>
    Rejected = 3,

    /// <summary>Cancelled (the approval round was withdrawn).</summary>
    Cancelled = 4,
}
