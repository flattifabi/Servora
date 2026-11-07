namespace Servora.Domain.Automation;

/// <summary>Outcome of a single step execution inside a flow run.</summary>
public enum FlowStepStatus
{
    /// <summary>The step is executing.</summary>
    Running = 0,

    /// <summary>The step completed successfully.</summary>
    Succeeded = 1,

    /// <summary>The step failed (after all retries).</summary>
    Failed = 2,

    /// <summary>The step waits for time, an approval or a sub-flow.</summary>
    Waiting = 3,

    /// <summary>The step was skipped (for example because the run was cancelled).</summary>
    Skipped = 4,
}
