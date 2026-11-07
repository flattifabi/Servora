using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>Full read model of a user for the user record page.</summary>
public sealed record UserDetail
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

    /// <summary>Gets the manager.</summary>
    public Guid? ManagerId { get; init; }

    /// <summary>Gets the preferred time zone.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Gets the account source.</summary>
    public UserSource Source { get; init; }

    /// <summary>Gets the external identifier (objectGUID / SID).</summary>
    public string? ExternalId { get; init; }

    /// <summary>Gets the directory distinguished name.</summary>
    public string? DistinguishedName { get; init; }

    /// <summary>Gets a value indicating whether the account is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether a password change is pending.</summary>
    public bool MustChangePassword { get; init; }

    /// <summary>Gets the lockout end, if locked.</summary>
    public DateTimeOffset? LockoutEndsAt { get; init; }

    /// <summary>Gets the last successful sign-in.</summary>
    public DateTimeOffset? LastSignInAt { get; init; }

    /// <summary>Gets the last directory synchronisation.</summary>
    public DateTimeOffset? LastSyncedAt { get; init; }

    /// <summary>Gets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the creator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Gets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the last updater.</summary>
    public string? UpdatedBy { get; init; }

    /// <summary>Gets the concurrency version.</summary>
    public long Version { get; init; }

    /// <summary>Gets the directly assigned roles.</summary>
    public IReadOnlyList<UserRoleItem> Roles { get; init; } = [];

    /// <summary>Gets the group memberships.</summary>
    public IReadOnlyList<UserGroupItem> Groups { get; init; } = [];

    /// <summary>Gets the roles received through groups.</summary>
    public IReadOnlyList<InheritedRoleItem> InheritedRoles { get; init; } = [];

    /// <summary>Gets the effective permission keys.</summary>
    public IReadOnlyList<string> EffectivePermissions { get; init; } = [];

    /// <summary>Gets a value indicating whether this is the bootstrap administrator.</summary>
    public bool IsBootstrapAdmin { get; init; }

    /// <summary>Gets a value indicating whether this is the acting user.</summary>
    public bool IsSelf { get; init; }

    /// <summary>Gets a value indicating whether the acting user may manage this account (escalation guard).</summary>
    public bool CanManage { get; init; }

    /// <summary>Gets a value indicating whether the account is currently locked out.</summary>
    public bool IsLockedOut => LockoutEndsAt is { } until && until > DateTimeOffset.UtcNow;
}
