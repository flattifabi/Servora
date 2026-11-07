namespace Servora.Application.Automation.Actions;

/// <summary>Outcome of a step execution.</summary>
public enum FlowActionOutcome
{
    /// <summary>The step completed.</summary>
    Succeeded = 0,

    /// <summary>The step failed.</summary>
    Failed = 1,

    /// <summary>The step pauses the run until <see cref="FlowActionResult.ResumeAt"/>.</summary>
    Waiting = 2,
}
