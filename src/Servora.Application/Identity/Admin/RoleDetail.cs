namespace Servora.Application.Identity.Admin;

/// <summary>Full read model of a role for the role editor.</summary>
public sealed record RoleDetail
{
    /// <summary>Gets the role identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the technical name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets a value indicating whether the role is built in.</summary>
    public bool IsSystem { get; init; }

    /// <summary>Gets the granted permission keys.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Gets users holding the role directly (bounded).</summary>
    public IReadOnlyList<RoleHolderItem> Users { get; init; } = [];

    /// <summary>Gets the total number of direct holders.</summary>
    public int UserCount { get; init; }

    /// <summary>Gets groups granting the role.</summary>
    public IReadOnlyList<RoleGroupItem> Groups { get; init; } = [];

    /// <summary>Gets directory groups mapped to the role.</summary>
    public IReadOnlyList<string> DirectoryMappings { get; init; } = [];

    /// <summary>Gets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the last updater.</summary>
    public string? UpdatedBy { get; init; }

    /// <summary>Gets the concurrency version.</summary>
    public long Version { get; init; }

    /// <summary>Gets a value indicating whether this is the protected administrator role (keeps <c>*</c>).</summary>
    public bool IsAdministratorRole { get; init; }
}
