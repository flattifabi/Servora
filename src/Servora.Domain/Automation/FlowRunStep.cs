using Servora.Domain.Common;

namespace Servora.Domain.Automation;

/// <summary>History entry of one step execution inside a <see cref="FlowRun"/>.</summary>
public class FlowRunStep : Entity
{
    /// <summary>Gets or sets the run.</summary>
    public Guid RunId { get; set; }

    /// <summary>Gets or sets the step identifier inside the flow definition.</summary>
    public string StepId { get; set; } = string.Empty;

    /// <summary>Gets or sets the step key used in data pills (<c>steps.&lt;key&gt;</c>).</summary>
    public string StepKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the step display name.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Gets or sets the action type key.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Gets or sets the execution sequence number inside the run.</summary>
    public int Sequence { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public FlowStepStatus Status { get; set; } = FlowStepStatus.Running;

    /// <summary>Gets or sets the attempt number (1 = first try).</summary>
    public int Attempt { get; set; } = 1;

    /// <summary>Gets or sets the UTC start time.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets the UTC completion time.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets the redacted, truncated input snapshot (resolved configuration) as JSON.</summary>
    public string? InputJson { get; set; }

    /// <summary>Gets or sets the redacted, truncated output snapshot as JSON.</summary>
    public string? OutputJson { get; set; }

    /// <summary>Gets or sets the user-safe error message.</summary>
    public string? Error { get; set; }
}
