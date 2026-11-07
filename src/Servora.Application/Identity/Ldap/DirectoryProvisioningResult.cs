using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>Result of <see cref="IDirectoryProvisioningService.ProvisionAsync"/>.</summary>
/// <param name="User">The local user (detached) when provisioned or updated.</param>
/// <param name="Created">Whether the user was created.</param>
/// <param name="Updated">Whether attributes of an existing user changed.</param>
/// <param name="Deactivated">Whether the user was deactivated because the directory account is disabled.</param>
/// <param name="MembershipChanges">The number of directory-managed role / group memberships added or removed.</param>
/// <param name="Skipped">Whether the entry was skipped (unknown user and creation not allowed).</param>
/// <param name="Error">A user-safe error message when provisioning was refused.</param>
public sealed record DirectoryProvisioningResult(
    User? User,
    bool Created = false,
    bool Updated = false,
    bool Deactivated = false,
    int MembershipChanges = 0,
    bool Skipped = false,
    string? Error = null)
{
    /// <summary>Gets a value indicating whether a usable local user is available.</summary>
    public bool Succeeded => User is not null && Error is null;
}
