using Servora.Domain.Common;

namespace Servora.Domain.Automation;

/// <summary>One execution of a <see cref="Flow"/>, including its persisted execution context for resuming.</summary>
public class FlowRun : Entity
{
    /// <summary>Gets or sets the flow.</summary>
    public Guid FlowId { get; set; }

    /// <summary>Gets or sets the flow name at start time.</summary>
    public string FlowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the flow definition version that is executed.</summary>
    public int FlowVersion { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public FlowRunStatus Status { get; set; } = FlowRunStatus.Queued;

    /// <summary>Gets or sets what started the run.</summary>
    public FlowTriggerType TriggerType { get; set; }

    /// <summary>Gets or sets a short, secret-free description of the trigger event.</summary>
    public string? TriggerSummary { get; set; }

    /// <summary>Gets or sets the table of the triggering record.</summary>
    public string? TriggerTable { get; set; }

    /// <summary>Gets or sets the triggering record.</summary>
    public Guid? TriggerRecordId { get; set; }

    /// <summary>Gets or sets the idempotency key; unique per flow so a trigger event never starts the same flow twice.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the parent run when this run is a sub-flow.</summary>
    public Guid? ParentRunId { get; set; }

    /// <summary>Gets or sets the sub-flow nesting depth (0 for top-level runs).</summary>
    public int Depth { get; set; }

    /// <summary>Gets or sets the record event cascade depth applied to record changes made by this run (loop guard).</summary>
    public int EventDepth { get; set; }

    /// <summary>Gets or sets the snapshot of the step tree executed by this run (later flow edits do not affect it).</summary>
    public string StepsJson { get; set; } = "[]";

    /// <summary>Gets or sets a value indicating whether this is a test run started from the designer.</summary>
    public bool IsTest { get; set; }

    /// <summary>Gets or sets the display name of the actor that started the run.</summary>
    public string? StartedBy { get; set; }

    /// <summary>Gets or sets the redacted execution context (trigger data, inputs, variables, step outputs) as JSON.</summary>
    public string ContextJson { get; set; } = "{}";

    /// <summary>Gets or sets the UTC time the run was queued.</summary>
    public DateTimeOffset QueuedAt { get; set; }

    /// <summary>Gets or sets the UTC time execution started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets the UTC completion time.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the UTC time a waiting run should be checked again.</summary>
    public DateTimeOffset? ResumeAt { get; set; }

    /// <summary>Gets or sets the identifier of the step the run waits on.</summary>
    public string? WaitingStepId { get; set; }

    /// <summary>Gets or sets a user-facing description of what the run waits for.</summary>
    public string? WaitReason { get; set; }

    /// <summary>Gets or sets a value indicating whether cancellation was requested.</summary>
    public bool CancelRequested { get; set; }

    /// <summary>Gets or sets the number of executed steps.</summary>
    public int StepCount { get; set; }

    /// <summary>Gets or sets the user-safe error message for failed runs.</summary>
    public string? Error { get; set; }
}
