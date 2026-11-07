using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>Membership of a <see cref="User"/> in a <see cref="Group"/>.</summary>
public class GroupMember : Entity
{
    /// <summary>Gets or sets the group identifier.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets the group navigation property.</summary>
    public Group? Group { get; set; }

    /// <summary>Gets or sets the user identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the user navigation property.</summary>
    public User? User { get; set; }

    /// <summary>Gets or sets a value indicating whether the membership was created by directory synchronisation.</summary>
    public bool IsFromDirectory { get; set; }
}
