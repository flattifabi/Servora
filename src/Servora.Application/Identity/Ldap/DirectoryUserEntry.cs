namespace Servora.Application.Identity.Ldap;

/// <summary>A user object read from the directory, already mapped through the configured attribute mapping.</summary>
/// <param name="DistinguishedName">The distinguished name.</param>
/// <param name="UserName">The login name (value of the user name attribute).</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="Email">The e-mail address.</param>
/// <param name="Phone">The phone number.</param>
/// <param name="Title">The job title.</param>
/// <param name="Department">The department.</param>
/// <param name="ExternalId">The stable unique identifier (objectGUID / entryUUID) as string.</param>
/// <param name="Sid">The Windows security identifier (S-1-5-…) when available.</param>
/// <param name="IsDisabled">Whether the account is disabled in the directory (AD userAccountControl ACCOUNTDISABLE).</param>
/// <param name="Groups">The distinguished names of the groups the user is a member of.</param>
public sealed record DirectoryUserEntry(
    string DistinguishedName,
    string UserName,
    string? DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Title,
    string? Department,
    string? ExternalId,
    string? Sid,
    bool IsDisabled,
    IReadOnlyList<string> Groups)
{
    /// <summary>Gets the best available display name.</summary>
    public string EffectiveDisplayName =>
        !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName!
        : string.Join(' ', new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } full ? full
        : UserName;
}
