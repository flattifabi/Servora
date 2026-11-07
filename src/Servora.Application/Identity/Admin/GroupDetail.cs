using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>Full read model of a group for the group record page.</summary>
public sealed record GroupDetail
{
    /// <summary>Gets the group identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the group mailbox.</summary>
    public string? Email { get; init; }

    /// <summary>Gets the group type.</summary>
    public GroupType Type { get; init; }

    /// <summary>Gets the manager.</summary>
    public Guid? ManagerId { get; init; }

    /// <summary>Gets the parent group.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the parent group name.</summary>
    public string? ParentName { get; init; }

    /// <summary>Gets where the group is mastered.</summary>
    public UserSource Source { get; init; }

    /// <summary>Gets the directory distinguished name.</summary>
    public string? DistinguishedName { get; init; }

    /// <summary>Gets a value indicating whether the group is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Gets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the last updater.</summary>
    public string? UpdatedBy { get; init; }

    /// <summary>Gets the concurrency version.</summary>
    public long Version { get; init; }

    /// <summary>Gets the members (bounded).</summary>
    public IReadOnlyList<GroupMemberItem> Members { get; init; } = [];

    /// <summary>Gets the total number of members.</summary>
    public int MemberCount { get; init; }

    /// <summary>Gets the roles granted to members.</summary>
    public IReadOnlyList<GroupRoleItem> Roles { get; init; } = [];

    /// <summary>Gets the names of directory groups mapped to this group.</summary>
    public IReadOnlyList<string> DirectoryMappings { get; init; } = [];
}
