namespace Servora.Application.Ai;

/// <summary>Resolves provider connections including decrypted API keys (backend use only).</summary>
public interface IAiConnectionResolver
{
    /// <summary>Gets the default enabled provider (or the first enabled one).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection, or <see langword="null"/> when no provider is configured.</returns>
    Task<AiProviderConnection?> GetDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a provider by id.</summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection, or <see langword="null"/>.</returns>
    Task<AiProviderConnection?> GetAsync(Guid providerId, CancellationToken cancellationToken = default);
}
