using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>A member of a group.</summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="UserName">The login name.</param>
/// <param name="Email">The e-mail address.</param>
/// <param name="Title">The job title.</param>
/// <param name="Source">The account source.</param>
/// <param name="IsActive">Whether the user is active.</param>
/// <param name="IsFromDirectory">Whether the membership is managed by a directory group mapping.</param>
public sealed record GroupMemberItem(
    Guid UserId,
    string DisplayName,
    string UserName,
    string? Email,
    string? Title,
    UserSource Source,
    bool IsActive,
    bool IsFromDirectory);
