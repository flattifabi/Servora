using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>A trace step as shown on a tool-call card.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Kind">The kind (<see cref="AiTraceKinds"/>).</param>
/// <param name="AgentKey">The agent key.</param>
/// <param name="AgentName">The agent display name.</param>
/// <param name="ToolName">The tool name or the delegated agent key.</param>
/// <param name="ArgumentsJson">The arguments.</param>
/// <param name="ResultJson">The bounded result.</param>
/// <param name="Status">The status.</param>
/// <param name="ErrorMessage">The error message.</param>
/// <param name="ActorName">The scoped identity.</param>
/// <param name="DurationMs">The duration.</param>
public sealed record AiToolCallView(
    Guid Id,
    string Kind,
    string AgentKey,
    string AgentName,
    string ToolName,
    string? ArgumentsJson,
    string? ResultJson,
    AiToolCallStatus Status,
    string? ErrorMessage,
    string? ActorName,
    long DurationMs);
