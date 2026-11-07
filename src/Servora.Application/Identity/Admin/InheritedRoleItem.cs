namespace Servora.Application.Identity.Admin;

/// <summary>A role a user receives through a group.</summary>
/// <param name="RoleId">The role identifier.</param>
/// <param name="RoleLabel">The role label.</param>
/// <param name="GroupId">The granting group.</param>
/// <param name="GroupName">The granting group name.</param>
public sealed record InheritedRoleItem(Guid RoleId, string RoleLabel, Guid GroupId, string GroupName);
