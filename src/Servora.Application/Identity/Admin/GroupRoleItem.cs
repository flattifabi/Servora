namespace Servora.Application.Identity.Admin;

/// <summary>A role granted by a group.</summary>
/// <param name="RoleId">The role identifier.</param>
/// <param name="Name">The technical role name.</param>
/// <param name="Label">The role label.</param>
/// <param name="IsSystem">Whether the role is built in.</param>
/// <param name="PermissionCount">The number of permission keys of the role.</param>
public sealed record GroupRoleItem(Guid RoleId, string Name, string Label, bool IsSystem, int PermissionCount);
