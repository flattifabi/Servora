namespace Servora.Application.Identity.Admin;

/// <summary>A row of the role list.</summary>
/// <param name="Id">The role identifier.</param>
/// <param name="Name">The technical name.</param>
/// <param name="Label">The label.</param>
/// <param name="Description">The description.</param>
/// <param name="IsSystem">Whether the role is built in.</param>
/// <param name="PermissionCount">The number of permission keys.</param>
/// <param name="HasWildcard">Whether the role grants the global wildcard.</param>
/// <param name="UserCount">Users holding the role directly.</param>
/// <param name="GroupCount">Groups granting the role.</param>
/// <param name="UpdatedAt">The last update time.</param>
public sealed record RoleListItem(
    Guid Id,
    string Name,
    string Label,
    string? Description,
    bool IsSystem,
    int PermissionCount,
    bool HasWildcard,
    int UserCount,
    int GroupCount,
    DateTimeOffset UpdatedAt);
