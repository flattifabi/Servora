using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>A row of the execution list.</summary>
/// <param name="Id">The run identifier.</param>
/// <param name="FlowId">The flow.</param>
/// <param name="FlowName">The flow name.</param>
/// <param name="Status">The status.</param>
/// <param name="TriggerType">The trigger type.</param>
/// <param name="TriggerSummary">The trigger description.</param>
/// <param name="IsTest">Whether it is a test run.</param>
/// <param name="QueuedAt">The queue time.</param>
/// <param name="StartedAt">The start time.</param>
/// <param name="CompletedAt">The completion time.</param>
/// <param name="StepCount">The number of executed steps.</param>
/// <param name="Error">The error message.</param>
/// <param name="WaitReason">The wait reason.</param>
public sealed record FlowRunListItem(
    Guid Id,
    Guid FlowId,
    string FlowName,
    FlowRunStatus Status,
    FlowTriggerType TriggerType,
    string? TriggerSummary,
    bool IsTest,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int StepCount,
    string? Error,
    string? WaitReason)
{
    /// <summary>Gets the duration in milliseconds for completed runs.</summary>
    public long? DurationMs => StartedAt is { } start && CompletedAt is { } end ? (long)(end - start).TotalMilliseconds : null;
}
