using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>A row of the group list.</summary>
/// <param name="Id">The group identifier.</param>
/// <param name="Name">The group name.</param>
/// <param name="Description">The description.</param>
/// <param name="Type">The group type.</param>
/// <param name="Email">The group mailbox.</param>
/// <param name="ManagerId">The manager.</param>
/// <param name="ManagerName">The manager display name.</param>
/// <param name="MemberCount">The number of members.</param>
/// <param name="Roles">Labels of roles granted to members.</param>
/// <param name="Source">Where the group is mastered.</param>
/// <param name="IsActive">Whether the group is active.</param>
public sealed record GroupListItem(
    Guid Id,
    string Name,
    string? Description,
    GroupType Type,
    string? Email,
    Guid? ManagerId,
    string? ManagerName,
    int MemberCount,
    IReadOnlyList<string> Roles,
    UserSource Source,
    bool IsActive);
