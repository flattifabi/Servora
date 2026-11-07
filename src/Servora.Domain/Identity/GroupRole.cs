using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>A <see cref="Role"/> granted to every member of a <see cref="Group"/>.</summary>
public class GroupRole : Entity
{
    /// <summary>Gets or sets the group identifier.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets the role identifier.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Gets or sets the role navigation property.</summary>
    public Role? Role { get; set; }
}
