using Servora.Application.Common;

namespace Servora.Application.Ai;

/// <summary>Provider-neutral chat completion entry point used by the orchestrator (dispatches to <see cref="IAiProviderClient"/>s).</summary>
public interface IAiChatClient
{
    /// <summary>Sends a chat completion request to the provider of the connection.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response or a user-safe failure.</returns>
    Task<Result<AiChatResponse>> CompleteAsync(AiProviderConnection connection, AiChatRequest request, CancellationToken cancellationToken = default);
}
