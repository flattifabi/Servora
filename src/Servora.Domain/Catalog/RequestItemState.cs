namespace Servora.Domain.Catalog;

/// <summary>State of a requested item.</summary>
public enum RequestItemState
{
    /// <summary>Waiting for approval.</summary>
    PendingApproval = 0,

    /// <summary>Approved (or no approval needed) and being fulfilled by catalog tasks.</summary>
    InFulfillment = 1,

    /// <summary>Delivered (final).</summary>
    Completed = 2,

    /// <summary>Rejected by the approvers (final).</summary>
    Rejected = 3,

    /// <summary>Cancelled (final).</summary>
    Cancelled = 4,
}
