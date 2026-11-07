using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>Input model for creating or editing a group.</summary>
public sealed class GroupEditModel
{
    /// <summary>Gets or sets the unique name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the group mailbox.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the group type.</summary>
    public GroupType Type { get; set; } = GroupType.Assignment;

    /// <summary>Gets or sets the manager.</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>Gets or sets the parent group.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets a value indicating whether the group is active.</summary>
    public bool IsActive { get; set; } = true;
}
