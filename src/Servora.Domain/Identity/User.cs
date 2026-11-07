using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>
/// A person or technical identity that can be referenced by records and, depending on <see cref="Source"/>,
/// sign in to the platform.
/// </summary>
[RecordTable(TableNames.User, "User", PluralLabel = "Users", DisplayField = nameof(DisplayName), Icon = "user", Route = "/admin/users")]
public class User : AuditableEntity, IHasCustomFields
{
    /// <summary>Gets or sets the unique login name (case-insensitive). For directory users this is the sAMAccountName / uid.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized (upper invariant) login name used for unique lookups.</summary>
    [NotExposed]
    public string NormalizedUserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the full display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the last name.</summary>
    public string? LastName { get; set; }

    /// <summary>Gets or sets the e-mail address used for notifications.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Gets or sets the job title.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the department name.</summary>
    public string? Department { get; set; }

    /// <summary>Gets or sets the location / site name.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the identifier of the user's manager.</summary>
    [Reference(TableNames.User)]
    public Guid? ManagerId { get; set; }

    /// <summary>Gets or sets where the account is mastered.</summary>
    public UserSource Source { get; set; } = UserSource.Local;

    /// <summary>
    /// Gets or sets the stable external identifier (AD objectGUID, LDAP entryUUID or Windows SID) for directory and Windows users.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>Gets or sets the distinguished name in the directory, if any.</summary>
    public string? DistinguishedName { get; set; }

    /// <summary>Gets or sets the password hash for local accounts. Never exposed through any API.</summary>
    [NotExposed]
    public string? PasswordHash { get; set; }

    /// <summary>Gets or sets a value indicating whether the user must change the password at next sign-in.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Gets or sets a value indicating whether the account is active. Inactive accounts cannot sign in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the number of consecutive failed sign-in attempts.</summary>
    [NotExposed]
    public int FailedSignInCount { get; set; }

    /// <summary>Gets or sets the UTC time until which the account is locked out.</summary>
    public DateTimeOffset? LockoutEndsAt { get; set; }

    /// <summary>Gets or sets the UTC time of the last successful sign-in.</summary>
    public DateTimeOffset? LastSignInAt { get; set; }

    /// <summary>Gets or sets the UTC time of the last directory synchronisation.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// Gets or sets a random value that changes whenever credentials or permissions change. Authentication cookies
    /// carrying an outdated stamp are rejected.
    /// </summary>
    [NotExposed]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the preferred IANA / Windows time zone identifier.</summary>
    public string? TimeZone { get; set; }

    /// <summary>Gets or sets the preferred UI theme (<c>dark</c>, <c>light</c> or <c>system</c>).</summary>
    public string? Theme { get; set; }

    /// <inheritdoc />
    public string? CustomFieldsJson { get; set; }

    /// <summary>Gets the role assignments of the user.</summary>
    public List<UserRole> Roles { get; set; } = [];

    /// <summary>Gets the group memberships of the user.</summary>
    public List<GroupMember> Groups { get; set; } = [];
}
