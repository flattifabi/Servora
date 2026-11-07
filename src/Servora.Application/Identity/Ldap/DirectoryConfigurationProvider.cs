using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>Default <see cref="IDirectoryConfigurationProvider"/>.</summary>
/// <param name="store">The identity settings store.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="dbFactory">The database context factory.</param>
public sealed class DirectoryConfigurationProvider(IdentitySettingsStore store, ISecretStore secrets, IAppDbContextFactory dbFactory) : IDirectoryConfigurationProvider
{
    /// <summary>Upper bound of mappings loaded per evaluation.</summary>
    public const int MaxMappings = 1000;

    /// <inheritdoc />
    public Task<DirectorySettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        store.GetAsync(DirectorySettings.SettingKey, () => new DirectorySettings(), cancellationToken);

    /// <inheritdoc />
    public Task<string?> GetBindPasswordAsync(CancellationToken cancellationToken = default) =>
        secrets.GetAsync(DirectorySettings.BindPasswordSecretName, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DirectoryGroupMapping>> GetActiveMappingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<DirectoryGroupMapping>().AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DirectoryGroup)
            .Take(MaxMappings)
            .ToListAsync(cancellationToken);
    }
}
