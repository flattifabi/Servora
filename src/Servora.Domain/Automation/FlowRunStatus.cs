namespace Servora.Domain.Automation;

/// <summary>Lifecycle state of a flow execution.</summary>
public enum FlowRunStatus
{
    /// <summary>Waiting for a free engine slot.</summary>
    Queued = 0,

    /// <summary>Currently executing.</summary>
    Running = 1,

    /// <summary>Paused on a wait, approval or sub-flow step; resumed by the background service.</summary>
    Waiting = 2,

    /// <summary>All steps completed.</summary>
    Succeeded = 3,

    /// <summary>A step failed after all retries.</summary>
    Failed = 4,

    /// <summary>Cancelled by a user.</summary>
    Cancelled = 5,
}
