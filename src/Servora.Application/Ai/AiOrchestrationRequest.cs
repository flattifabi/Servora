namespace Servora.Application.Ai;

/// <summary>Input of an orchestration run.</summary>
/// <param name="UserId">The requesting user.</param>
/// <param name="UserDisplayName">The user's display name.</param>
/// <param name="History">Previous messages of the conversation (bounded, oldest first).</param>
/// <param name="Message">The new user message.</param>
/// <param name="Connection">The provider connection.</param>
public sealed record AiOrchestrationRequest(Guid UserId, string UserDisplayName, IReadOnlyList<AiChatMessage> History, string Message, AiProviderConnection Connection)
{
    /// <summary>Gets the keys of agents disabled by an administrator.</summary>
    public IReadOnlySet<string> DisabledAgents { get; init; } = new HashSet<string>();

    /// <summary>Gets the correlation id for the agent scopes (audit).</summary>
    public string? CorrelationId { get; init; }
}
