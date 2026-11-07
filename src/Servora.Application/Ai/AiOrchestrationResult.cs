namespace Servora.Application.Ai;

/// <summary>Output of an orchestration run.</summary>
/// <param name="Answer">The final answer for the user.</param>
/// <param name="AgentKey">The (primary) agent that handled the request, or <see langword="null"/> when answered directly.</param>
/// <param name="Steps">The execution trace.</param>
/// <param name="InputTokens">Total input tokens.</param>
/// <param name="OutputTokens">Total output tokens.</param>
public sealed record AiOrchestrationResult(string Answer, string? AgentKey, IReadOnlyList<AiTraceStep> Steps, int InputTokens, int OutputTokens);
