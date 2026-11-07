namespace Servora.Application.Automation;

/// <summary>Status of a flow execution.</summary>
/// <param name="Id">The run identifier.</param>
/// <param name="FlowId">The flow identifier.</param>
/// <param name="FlowName">The flow name.</param>
/// <param name="Status">The status: Queued, Running, Waiting, Succeeded, Failed or Cancelled.</param>
/// <param name="StartedAt">The UTC start time.</param>
/// <param name="CompletedAt">The UTC completion time.</param>
/// <param name="Error">A user-safe error message for failed runs.</param>
public sealed record FlowRunSummary(Guid Id, Guid FlowId, string FlowName, string Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, string? Error);
