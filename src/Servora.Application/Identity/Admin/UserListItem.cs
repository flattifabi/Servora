using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>A row of the user list.</summary>
/// <param name="Id">The user identifier.</param>
/// <param name="UserName">The login name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Email">The e-mail address.</param>
/// <param name="Title">The job title.</param>
/// <param name="Department">The department.</param>
/// <param name="Source">The account source.</param>
/// <param name="IsActive">Whether the account is active.</param>
/// <param name="IsLockedOut">Whether the account is currently locked out.</param>
/// <param name="MustChangePassword">Whether a password change is pending.</param>
/// <param name="LastSignInAt">The last successful sign-in.</param>
/// <param name="Roles">Labels of directly assigned roles.</param>
/// <param name="GroupCount">The number of group memberships.</param>
public sealed record UserListItem(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Email,
    string? Title,
    string? Department,
    UserSource Source,
    bool IsActive,
    bool IsLockedOut,
    bool MustChangePassword,
    DateTimeOffset? LastSignInAt,
    IReadOnlyList<string> Roles,
    int GroupCount);
