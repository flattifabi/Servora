namespace Servora.Application.Identity.Admin;

/// <summary>A role for pickers.</summary>
/// <param name="Id">The role identifier.</param>
/// <param name="Name">The technical name.</param>
/// <param name="Label">The label.</param>
/// <param name="IsSystem">Whether the role is built in.</param>
/// <param name="PermissionCount">The number of permission keys.</param>
public sealed record RoleOption(Guid Id, string Name, string Label, bool IsSystem, int PermissionCount);
