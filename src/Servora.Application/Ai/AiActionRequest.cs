namespace Servora.Application.Ai;

/// <summary>A consequential action the user confirmed in the UI.</summary>
/// <param name="UserId">The confirming user.</param>
/// <param name="UserDisplayName">The user's display name.</param>
/// <param name="AgentKey">The agent that proposed the action.</param>
/// <param name="ToolName">The tool.</param>
/// <param name="ArgumentsJson">The proposed arguments.</param>
public sealed record AiActionRequest(Guid UserId, string UserDisplayName, string AgentKey, string ToolName, string ArgumentsJson)
{
    /// <summary>Gets the correlation id for the agent scope (audit).</summary>
    public string? CorrelationId { get; init; }
}
