using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Creates or updates local users from directory entries and reconciles directory-managed role and group memberships.
/// Backend service used by LDAP sign-in, Windows SSO and synchronisation; it performs no permission check because it
/// runs before a user is authenticated or as the system actor.
/// </summary>
public interface IDirectoryProvisioningService
{
    /// <summary>Provisions a directory entry.</summary>
    /// <param name="entry">The directory user entry.</param>
    /// <param name="settings">The directory settings.</param>
    /// <param name="mappings">The active group mappings.</param>
    /// <param name="allowCreate">Whether an unknown user may be created.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provisioning result.</returns>
    Task<DirectoryProvisioningResult> ProvisionAsync(
        DirectoryUserEntry entry,
        DirectorySettings settings,
        IReadOnlyList<DirectoryGroupMapping> mappings,
        bool allowCreate,
        CancellationToken cancellationToken = default);
}
