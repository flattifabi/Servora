using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>Direct assignment of a <see cref="Role"/> to a <see cref="User"/>.</summary>
public class UserRole : Entity
{
    /// <summary>Gets or sets the user identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the role identifier.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Gets or sets the role navigation property.</summary>
    public Role? Role { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the assignment was created by directory synchronisation. Synced
    /// assignments are removed automatically when the directory group membership disappears.
    /// </summary>
    public bool IsFromDirectory { get; set; }
}
