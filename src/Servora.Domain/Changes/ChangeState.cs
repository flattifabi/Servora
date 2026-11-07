namespace Servora.Domain.Changes;

/// <summary>Lifecycle state of a change request.</summary>
public enum ChangeState
{
    /// <summary>Drafted, not yet submitted for assessment.</summary>
    New = 0,

    /// <summary>Technical assessment and risk evaluation.</summary>
    Assess = 1,

    /// <summary>Waiting for authorization by the change advisory board.</summary>
    Authorize = 2,

    /// <summary>Authorized and scheduled for implementation.</summary>
    Scheduled = 3,

    /// <summary>Implementation in progress.</summary>
    Implement = 4,

    /// <summary>Post implementation review.</summary>
    Review = 5,

    /// <summary>Closed (final).</summary>
    Closed = 6,

    /// <summary>Cancelled (final).</summary>
    Cancelled = 7,
}
