namespace Servora.Application.Identity.Ldap;

/// <summary>A login name as typed by a user, split into account, NetBIOS domain and user principal name.</summary>
/// <param name="Account">The account name (sAMAccountName / uid) without domain.</param>
/// <param name="Domain">The NetBIOS domain from a <c>DOMAIN\user</c> login, if any.</param>
/// <param name="UserPrincipalName">The full UPN from a <c>user@domain</c> login, if any.</param>
public sealed record DirectoryLoginName(string Account, string? Domain, string? UserPrincipalName)
{
    /// <summary>The maximum accepted login length.</summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Parses a login name. Accepts <c>user</c>, <c>DOMAIN\user</c> and <c>user@domain</c>. Returns
    /// <see langword="null"/> for empty, overlong or malformed input (for example several separators).
    /// </summary>
    /// <param name="input">The typed login name.</param>
    /// <returns>The parsed name or <see langword="null"/>.</returns>
    public static DirectoryLoginName? Parse(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0 || value.Length > MaxLength || value.Any(char.IsControl))
        {
            return null;
        }

        var backslash = value.IndexOf('\\');
        if (backslash >= 0)
        {
            if (backslash != value.LastIndexOf('\\') || value.Contains('@'))
            {
                return null;
            }

            var domain = value[..backslash].Trim();
            var account = value[(backslash + 1)..].Trim();
            return domain.Length == 0 || account.Length == 0 ? null : new DirectoryLoginName(account, domain, null);
        }

        var at = value.IndexOf('@');
        if (at >= 0)
        {
            if (at != value.LastIndexOf('@') || at == 0 || at == value.Length - 1)
            {
                return null;
            }

            return new DirectoryLoginName(value[..at], null, value);
        }

        return new DirectoryLoginName(value, null, null);
    }

    /// <summary>
    /// Checks the NetBIOS domain against the configured one. A login without domain always matches; a login with a
    /// domain only matches when no domain is configured or the names are equal (case-insensitive).
    /// </summary>
    /// <param name="configuredDomain">The configured NetBIOS domain.</param>
    /// <returns><see langword="true"/> when the domain is acceptable.</returns>
    public bool MatchesDomain(string? configuredDomain) =>
        Domain is null || string.IsNullOrWhiteSpace(configuredDomain) || string.Equals(Domain, configuredDomain.Trim(), StringComparison.OrdinalIgnoreCase);
}
