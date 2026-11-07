namespace Servora.Application.Automation.Services;

/// <summary>A run with its step timeline and (redacted) data.</summary>
/// <param name="Run">The run.</param>
/// <param name="Steps">The step executions in order.</param>
/// <param name="ContextJson">The redacted public run data (indented JSON).</param>
/// <param name="ParentRunId">The parent run for sub-flows.</param>
/// <param name="ChildRunIds">Sub-flow runs started by this run.</param>
/// <param name="ResumeAt">When a waiting run is checked again.</param>
/// <param name="FlowVersion">The executed definition version.</param>
/// <param name="StartedBy">The starting actor.</param>
/// <param name="CancelRequested">Whether cancellation was requested.</param>
public sealed record FlowRunDetail(
    FlowRunListItem Run,
    IReadOnlyList<FlowRunStepItem> Steps,
    string ContextJson,
    Guid? ParentRunId,
    IReadOnlyList<Guid> ChildRunIds,
    DateTimeOffset? ResumeAt,
    int FlowVersion,
    string? StartedBy,
    bool CancelRequested);
