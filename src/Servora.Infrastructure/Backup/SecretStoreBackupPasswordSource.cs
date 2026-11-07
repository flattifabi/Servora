using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Backup;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Infrastructure.Backup;

/// <summary>Reads the archive password from <see cref="ISecretStore"/> in a scope of its own, acting as the system.</summary>
/// <param name="scopes">The scope factory.</param>
public sealed class SecretStoreBackupPasswordSource(IServiceScopeFactory scopes) : IBackupPasswordSource
{
    /// <inheritdoc />
    public async Task<string?> GetPasswordAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Backup"));
        return await scope.ServiceProvider.GetRequiredService<ISecretStore>().GetAsync(BackupConstants.PasswordSecretName, cancellationToken);
    }
}
