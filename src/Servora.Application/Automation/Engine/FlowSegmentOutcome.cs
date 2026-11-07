namespace Servora.Application.Automation.Engine;

/// <summary>Outcome of executing a list of steps within one execution segment of a run.</summary>
internal enum FlowSegmentOutcome
{
    /// <summary>All steps of the list completed.</summary>
    Completed = 0,

    /// <summary>A step paused the run.</summary>
    Waiting = 1,

    /// <summary>A step failed.</summary>
    Failed = 2,

    /// <summary>The run was cancelled.</summary>
    Cancelled = 3,
}
