using Servora.Domain.Identity;

namespace Servora.Application.Identity.Profile;

/// <summary>A group membership of the signed-in user.</summary>
/// <param name="Name">The group name.</param>
/// <param name="Type">The group type.</param>
/// <param name="Description">The description.</param>
/// <param name="IsFromDirectory">Whether the membership comes from the directory.</param>
public sealed record MyGroup(string Name, GroupType Type, string? Description, bool IsFromDirectory);
