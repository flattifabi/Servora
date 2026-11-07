using Servora.Application.Common;

namespace Servora.Application.Ai;

/// <summary>Conversations of the current user with the AI assistant (requires <c>ai.assistant.use</c>).</summary>
public interface IAiAssistantService
{
    /// <summary>Gets the assistant readiness (provider configured, enabled agents).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The status.</returns>
    Task<Result<AiAssistantStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the user's conversations, newest first.</summary>
    /// <param name="search">An optional title filter.</param>
    /// <param name="take">The maximum number (1–100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The conversations.</returns>
    Task<Result<IReadOnlyList<AiConversationSummary>>> ListConversationsAsync(string? search = null, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets a conversation of the user.</summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The conversation.</returns>
    Task<Result<AiConversationView>> GetConversationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Sends a message (creating a conversation when <paramref name="conversationId"/> is null) and runs the orchestrator.</summary>
    /// <param name="conversationId">The conversation, or <see langword="null"/>.</param>
    /// <param name="message">The message (max 4000 characters).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated conversation.</returns>
    Task<Result<AiConversationView>> SendAsync(Guid? conversationId, string message, CancellationToken cancellationToken = default);

    /// <summary>Confirms and executes a proposed consequential action.</summary>
    /// <param name="toolCallId">The pending trace step.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated conversation.</returns>
    Task<Result<AiConversationView>> ConfirmActionAsync(Guid toolCallId, CancellationToken cancellationToken = default);

    /// <summary>Rejects a proposed consequential action.</summary>
    /// <param name="toolCallId">The pending trace step.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated conversation.</returns>
    Task<Result<AiConversationView>> RejectActionAsync(Guid toolCallId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a conversation of the user.</summary>
    /// <param name="id">The conversation identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteConversationAsync(Guid id, CancellationToken cancellationToken = default);
}
