using Servora.Domain.Identity;

namespace Servora.Application.Identity.Profile;

/// <summary>The signed-in user's own profile (<c>/profile</c>).</summary>
public sealed record MyProfile
{
    /// <summary>Gets the user identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the login name.</summary>
    public required string UserName { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the first name.</summary>
    public string? FirstName { get; init; }

    /// <summary>Gets the last name.</summary>
    public string? LastName { get; init; }

    /// <summary>Gets the e-mail address.</summary>
    public string? Email { get; init; }

    /// <summary>Gets the phone number.</summary>
    public string? Phone { get; init; }

    /// <summary>Gets the job title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the department.</summary>
    public string? Department { get; init; }

    /// <summary>Gets the location.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the manager display name.</summary>
    public string? ManagerName { get; init; }

    /// <summary>Gets the preferred time zone.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Gets the preferred theme (<c>dark</c>, <c>light</c>, <c>system</c>).</summary>
    public string? Theme { get; init; }

    /// <summary>Gets the account source.</summary>
    public UserSource Source { get; init; }

    /// <summary>Gets the last successful sign-in.</summary>
    public DateTimeOffset? LastSignInAt { get; init; }

    /// <summary>Gets the account creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the roles (direct and through groups) with their origin.</summary>
    public IReadOnlyList<MyRole> Roles { get; init; } = [];

    /// <summary>Gets the group memberships.</summary>
    public IReadOnlyList<MyGroup> Groups { get; init; } = [];

    /// <summary>Gets the number of effective permission keys.</summary>
    public int PermissionCount { get; init; }

    /// <summary>Gets a value indicating whether the user holds the global wildcard.</summary>
    public bool IsAdministrator { get; init; }

    /// <summary>Gets a value indicating whether the user can change the password in Servora (local accounts).</summary>
    public bool CanChangePassword => Source == UserSource.Local;

    /// <summary>Gets a value indicating whether name fields are mastered by the directory.</summary>
    public bool IsDirectoryManaged => Source == UserSource.Directory;
}
