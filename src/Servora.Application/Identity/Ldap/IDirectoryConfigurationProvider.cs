using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Backend read access to the directory configuration for the sign-in pipeline, Windows SSO and the synchronisation
/// job. Not a UI service: it performs no permission check. Never expose <see cref="GetBindPasswordAsync"/> results
/// to UI read models or logs.
/// </summary>
public interface IDirectoryConfigurationProvider
{
    /// <summary>Gets the directory settings (cached briefly).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A copy of the settings.</returns>
    Task<DirectorySettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the decrypted bind password (backend use only).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The password or <see langword="null"/> when none is stored.</returns>
    Task<string?> GetBindPasswordAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the active directory group mappings.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mappings (detached entities).</returns>
    Task<IReadOnlyList<DirectoryGroupMapping>> GetActiveMappingsAsync(CancellationToken cancellationToken = default);
}
