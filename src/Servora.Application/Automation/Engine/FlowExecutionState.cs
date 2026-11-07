namespace Servora.Application.Automation.Engine;

/// <summary>Mutable state of one execution segment of a run (from start or resume until completion or the next wait).</summary>
internal sealed class FlowExecutionState
{
    /// <summary>Gets the run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Gets the flow identifier.</summary>
    public required Guid FlowId { get; init; }

    /// <summary>Gets the flow name.</summary>
    public required string FlowName { get; init; }

    /// <summary>Gets the sub-flow depth.</summary>
    public int Depth { get; init; }

    /// <summary>Gets a value indicating whether the run is a test run.</summary>
    public bool IsTest { get; init; }

    /// <summary>Gets the run data.</summary>
    public required FlowRunContext Context { get; init; }

    /// <summary>Gets or sets the next step history sequence number.</summary>
    public int Sequence { get; set; }

    /// <summary>Gets or sets the number of step executions in this segment.</summary>
    public int Executions { get; set; }

    /// <summary>Gets or sets the failure message.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the step the run waits on.</summary>
    public string? WaitStepId { get; set; }

    /// <summary>Gets or sets when the waiting run should be checked again.</summary>
    public DateTimeOffset? ResumeAt { get; set; }

    /// <summary>Gets or sets the wait reason.</summary>
    public string? WaitReason { get; set; }
}
