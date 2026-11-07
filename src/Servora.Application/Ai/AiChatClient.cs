using Servora.Application.Common;

namespace Servora.Application.Ai;

/// <summary>Default <see cref="IAiChatClient"/>: selects the provider client by wire protocol.</summary>
/// <param name="clients">The registered provider clients.</param>
public sealed class AiChatClient(IEnumerable<IAiProviderClient> clients) : IAiChatClient
{
    /// <inheritdoc />
    public Task<Result<AiChatResponse>> CompleteAsync(AiProviderConnection connection, AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var client = clients.LastOrDefault(c => c.Kind == connection.Kind);
        return client is null
            ? Task.FromResult(Result<AiChatResponse>.Failure($"No client is registered for provider type {connection.Kind}."))
            : client.CompleteAsync(connection, request, cancellationToken);
    }
}
