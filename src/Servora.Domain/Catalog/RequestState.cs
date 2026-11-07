namespace Servora.Domain.Catalog;

/// <summary>State of a service catalog request (derived from its requested items).</summary>
public enum RequestState
{
    /// <summary>Submitted, waiting for approval.</summary>
    Submitted = 0,

    /// <summary>Being fulfilled.</summary>
    InProgress = 1,

    /// <summary>All items delivered (final).</summary>
    Completed = 2,

    /// <summary>Rejected by the approvers (final).</summary>
    Rejected = 3,

    /// <summary>Cancelled (final).</summary>
    Cancelled = 4,
}
