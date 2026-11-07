using Servora.Application.Common;
using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>HTTP client of one provider wire protocol (implemented in Infrastructure with plain <c>HttpClient</c>).</summary>
public interface IAiProviderClient
{
    /// <summary>Gets the supported wire protocol.</summary>
    AiProviderKind Kind { get; }

    /// <summary>Sends a chat completion request.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response or a user-safe failure (never containing the API key).</returns>
    Task<Result<AiChatResponse>> CompleteAsync(AiProviderConnection connection, AiChatRequest request, CancellationToken cancellationToken);
}
