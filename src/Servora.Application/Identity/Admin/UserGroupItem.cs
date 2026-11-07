using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>A group membership of a user.</summary>
/// <param name="GroupId">The group identifier.</param>
/// <param name="Name">The group name.</param>
/// <param name="Type">The group type.</param>
/// <param name="IsFromDirectory">Whether the membership is managed by a directory group mapping.</param>
/// <param name="IsGroupActive">Whether the group is active.</param>
public sealed record UserGroupItem(Guid GroupId, string Name, GroupType Type, bool IsFromDirectory, bool IsGroupActive);
