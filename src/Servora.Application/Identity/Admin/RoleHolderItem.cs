namespace Servora.Application.Identity.Admin;

/// <summary>A user holding a role directly.</summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="UserName">The login name.</param>
/// <param name="IsActive">Whether the user is active.</param>
/// <param name="IsFromDirectory">Whether the assignment is managed by a directory group mapping.</param>
public sealed record RoleHolderItem(Guid UserId, string DisplayName, string UserName, bool IsActive, bool IsFromDirectory);
