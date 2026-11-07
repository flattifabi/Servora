using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>One step execution of a run (redacted snapshots).</summary>
/// <param name="Id">The history entry identifier.</param>
/// <param name="StepId">The step identifier.</param>
/// <param name="StepKey">The step key.</param>
/// <param name="StepName">The step name.</param>
/// <param name="ActionType">The action type key.</param>
/// <param name="Sequence">The sequence number.</param>
/// <param name="Status">The status.</param>
/// <param name="Attempt">The attempt number.</param>
/// <param name="StartedAt">The start time.</param>
/// <param name="CompletedAt">The completion time.</param>
/// <param name="DurationMs">The duration.</param>
/// <param name="InputJson">The redacted input snapshot.</param>
/// <param name="OutputJson">The redacted output snapshot.</param>
/// <param name="Error">The error message.</param>
public sealed record FlowRunStepItem(
    Guid Id,
    string StepId,
    string StepKey,
    string StepName,
    string ActionType,
    int Sequence,
    FlowStepStatus Status,
    int Attempt,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    long DurationMs,
    string? InputJson,
    string? OutputJson,
    string? Error);
