using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>
/// Maps a directory (LDAP / Active Directory) group to a Servora <see cref="Role"/> and / or a local <see cref="Group"/>.
/// Members of the directory group receive the role (<see cref="UserRole.IsFromDirectory"/>) and the local group
/// membership (<see cref="GroupMember.IsFromDirectory"/>) at sign-in and during directory synchronisation.
/// </summary>
public class DirectoryGroupMapping : AuditableEntity
{
    /// <summary>
    /// Gets or sets the directory group: either a full distinguished name
    /// (<c>CN=Servora Admins,OU=Groups,DC=corp,DC=example</c>) or a common name (<c>Servora Admins</c>).
    /// </summary>
    public string DirectoryGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the role granted to members of the directory group.</summary>
    [Reference(TableNames.Role)]
    public Guid? RoleId { get; set; }

    /// <summary>Gets or sets the local group that members of the directory group are added to.</summary>
    [Reference(TableNames.Group)]
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets an optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets a value indicating whether the mapping is applied.</summary>
    public bool IsActive { get; set; } = true;
}
