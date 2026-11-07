namespace Servora.Application.Identity.Admin;

/// <summary>A group granting a role.</summary>
/// <param name="GroupId">The group identifier.</param>
/// <param name="Name">The group name.</param>
/// <param name="MemberCount">The number of members.</param>
/// <param name="IsActive">Whether the group is active.</param>
public sealed record RoleGroupItem(Guid GroupId, string Name, int MemberCount, bool IsActive);
