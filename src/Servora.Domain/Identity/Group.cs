using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>A team of users. Groups can own work, approve records and grant roles to their members.</summary>
[RecordTable(TableNames.Group, "Group", PluralLabel = "Groups", DisplayField = nameof(Name), Icon = "users", Route = "/admin/groups")]
public class Group : AuditableEntity, IHasCustomFields
{
    /// <summary>Gets or sets the unique group name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the shared mailbox used for group notifications.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the group type.</summary>
    public GroupType Type { get; set; } = GroupType.Assignment;

    /// <summary>Gets or sets the group manager.</summary>
    [Reference(TableNames.User)]
    public Guid? ManagerId { get; set; }

    /// <summary>Gets or sets the parent group (for hierarchies).</summary>
    [Reference(TableNames.Group)]
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets where the group is mastered.</summary>
    public UserSource Source { get; set; } = UserSource.Local;

    /// <summary>Gets or sets the directory distinguished name for directory groups.</summary>
    public string? DistinguishedName { get; set; }

    /// <summary>Gets or sets the stable external identifier (objectGUID / entryUUID) for directory groups.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Gets or sets a value indicating whether the group is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc />
    public string? CustomFieldsJson { get; set; }

    /// <summary>Gets the members of the group.</summary>
    public List<GroupMember> Members { get; set; } = [];

    /// <summary>Gets the roles granted to every member of the group.</summary>
    public List<GroupRole> Roles { get; set; } = [];
}
