namespace Servora.Application.Identity.Admin;

/// <summary>A role assigned directly to a user.</summary>
/// <param name="RoleId">The role identifier.</param>
/// <param name="Name">The technical role name.</param>
/// <param name="Label">The role label.</param>
/// <param name="IsSystem">Whether the role is a built-in system role.</param>
/// <param name="IsFromDirectory">Whether the assignment is managed by a directory group mapping.</param>
public sealed record UserRoleItem(Guid RoleId, string Name, string Label, bool IsSystem, bool IsFromDirectory);
