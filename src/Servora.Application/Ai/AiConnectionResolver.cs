using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>Default <see cref="IAiConnectionResolver"/> (provider configuration plus key from <see cref="ISecretStore"/>).</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="secrets">The secret store.</param>
public sealed class AiConnectionResolver(IAppDbContextFactory dbFactory, ISecretStore secrets) : IAiConnectionResolver
{
    /// <inheritdoc />
    public async Task<AiProviderConnection?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var provider = await db.Set<AiProviderConfig>().AsNoTracking()
            .Where(p => p.IsEnabled)
            .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return provider is null ? null : await ToConnectionAsync(provider, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AiProviderConnection?> GetAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var provider = await db.Set<AiProviderConfig>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        return provider is null ? null : await ToConnectionAsync(provider, cancellationToken);
    }

    private async Task<AiProviderConnection> ToConnectionAsync(AiProviderConfig provider, CancellationToken cancellationToken) => new()
    {
        ProviderId = provider.Id,
        Name = provider.Name,
        Kind = provider.Kind,
        BaseUrl = provider.BaseUrl,
        Model = provider.Model,
        Deployment = provider.Deployment,
        ApiVersion = provider.ApiVersion,
        ApiKey = await secrets.GetAsync(provider.SecretName, cancellationToken),
        MaxTokens = provider.MaxTokens,
        Temperature = provider.Temperature,
        TimeoutSeconds = provider.TimeoutSeconds,
        SupportsTools = provider.SupportsTools,
    };
}
