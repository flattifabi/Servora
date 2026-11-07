namespace Servora.Application.Approvals;

/// <summary>Filters of the approval inbox.</summary>
public enum ApprovalInboxFilter
{
    /// <summary>My approvals waiting for a decision.</summary>
    MyPending = 0,

    /// <summary>Approvals I decided (or that were withdrawn).</summary>
    MyHistory = 1,

    /// <summary>All pending approvals (requires <c>approval.read</c>).</summary>
    AllPending = 2,

    /// <summary>All approvals (requires <c>approval.read</c>).</summary>
    All = 3,
}
