namespace Servora.Application.Identity.Ldap;

/// <summary>Validates <see cref="DirectorySettings"/> before they are saved or used for a connection test.</summary>
public static class DirectorySettingsValidator
{
    /// <summary>Validates the settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns><see langword="null"/> when valid, otherwise a user-safe message.</returns>
    public static string? Validate(DirectorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var host = settings.Host.Trim();
        var needsConnection = settings.Enabled || host.Length > 0;
        if (needsConnection)
        {
            if (host.Length == 0)
            {
                return "Enter the directory server host name.";
            }

            if (host.Length > 255 || host.Contains("://", StringComparison.Ordinal) || host.Any(c => char.IsWhiteSpace(c) || c is '/' or '\\' or '?' or '#' or '@'))
            {
                return "The host must be a plain DNS name or IP address without scheme or path (for example dc01.corp.example.com).";
            }

            if (settings.Port is < 1 or > 65535)
            {
                return "The port must be between 1 and 65535.";
            }

            if (string.IsNullOrWhiteSpace(settings.BaseDn) || !settings.BaseDn.Contains('='))
            {
                return "Enter the base DN, for example DC=corp,DC=example,DC=com.";
            }

            if (string.IsNullOrWhiteSpace(settings.BindDn))
            {
                return "A service account (bind DN) is required. Anonymous binds are not supported.";
            }
        }

        if (settings.TimeoutSeconds is < 1 or > 120)
        {
            return "The timeout must be between 1 and 120 seconds.";
        }

        if (Longer(1024, settings.BaseDn, settings.BindDn, settings.UserSearchBase, settings.GroupSearchBase))
        {
            return "Distinguished names must not exceed 1024 characters.";
        }

        if (settings.NetBiosDomain is { Length: > 15 } || settings.NetBiosDomain?.Any(c => char.IsWhiteSpace(c) || c is '\\' or '/' or '@') == true)
        {
            return "The NetBIOS domain must be a short name such as CORP (max. 15 characters).";
        }

        if (!LdapFilter.IsValidFilter(settings.UserFilter))
        {
            return "The user filter is not a valid LDAP filter. It must be enclosed in parentheses, for example (&(objectCategory=person)(objectClass=user)).";
        }

        if (!LdapFilter.IsValidFilter(settings.GroupFilter))
        {
            return "The group filter is not a valid LDAP filter, for example (objectClass=group).";
        }

        var attributes = new (string Label, string? Value, bool Required)[]
        {
            ("User name attribute", settings.UserNameAttribute, true),
            ("UPN attribute", settings.UpnAttribute, false),
            ("Display name attribute", settings.DisplayNameAttribute, true),
            ("First name attribute", settings.FirstNameAttribute, true),
            ("Last name attribute", settings.LastNameAttribute, true),
            ("E-mail attribute", settings.EmailAttribute, true),
            ("Phone attribute", settings.PhoneAttribute, true),
            ("Title attribute", settings.TitleAttribute, true),
            ("Department attribute", settings.DepartmentAttribute, true),
            ("Unique ID attribute", settings.ExternalIdAttribute, true),
            ("Member-of attribute", settings.MemberOfAttribute, true),
        };
        foreach (var (label, value, required) in attributes)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    return $"{label} is required.";
                }

                continue;
            }

            if (!LdapFilter.IsValidAttributeName(value.Trim()))
            {
                return $"{label} '{value}' is not a valid LDAP attribute name.";
            }
        }

        if (settings.SyncIntervalMinutes is < 15 or > 10080)
        {
            return "The synchronisation interval must be between 15 minutes and 7 days.";
        }

        if (settings.MaxSyncEntries is < 1 or > 100_000)
        {
            return "The maximum number of synchronised entries must be between 1 and 100,000.";
        }

        if (settings.DefaultRoleName is { Length: > 100 })
        {
            return "The default role name is too long.";
        }

        return null;
    }

    private static bool Longer(int max, params string?[] values) => values.Any(v => v is not null && v.Length > max);
}
