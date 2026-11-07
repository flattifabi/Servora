using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>One step of an orchestration run (persisted as <see cref="AiToolCall"/>).</summary>
/// <param name="Kind">The step kind (<see cref="AiTraceKinds"/>).</param>
/// <param name="AgentKey">The agent that performed the step (<c>orchestrator</c> for delegations).</param>
/// <param name="ToolName">The tool name or the target agent key.</param>
/// <param name="ArgumentsJson">The arguments.</param>
/// <param name="ResultText">The bounded result.</param>
/// <param name="Status">The status.</param>
/// <param name="Error">A user-safe error message.</param>
/// <param name="ActorName">The scoped identity that ran the step.</param>
/// <param name="DurationMs">The duration.</param>
public sealed record AiTraceStep(
    string Kind,
    string AgentKey,
    string ToolName,
    string? ArgumentsJson,
    string? ResultText,
    AiToolCallStatus Status,
    string? Error,
    string? ActorName,
    long DurationMs);
