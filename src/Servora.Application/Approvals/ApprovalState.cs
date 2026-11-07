namespace Servora.Application.Approvals;

/// <summary>Aggregated approval state of a record.</summary>
public enum ApprovalState
{
    /// <summary>No approval was requested.</summary>
    NotRequested = 0,

    /// <summary>Approval is pending.</summary>
    Requested = 1,

    /// <summary>Approved.</summary>
    Approved = 2,

    /// <summary>Rejected.</summary>
    Rejected = 3,

    /// <summary>Cancelled (for example because the record was withdrawn).</summary>
    Cancelled = 4,
}
