namespace Servora.Application.Identity.Ldap;

/// <summary>
/// LDAP / Active Directory and Windows integrated authentication settings (<c>/admin/directory</c>). Stored as the
/// system property <see cref="SettingKey"/>; the bind password is stored separately in the secret store under
/// <see cref="BindPasswordSecretName"/> and is never part of this object.
/// </summary>
public sealed record DirectorySettings
{
    /// <summary>The system property key.</summary>
    public const string SettingKey = "identity.directory.settings";

    /// <summary>The secret store name of the bind (service account) password.</summary>
    public const string BindPasswordSecretName = "identity.ldap.bind_password";

    // ----- Connection -----

    /// <summary>Gets or sets a value indicating whether LDAP sign-in and synchronisation are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the directory server host name (a domain controller or the domain DNS name).</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Gets or sets the TCP port (636 for LDAPS, 389 for StartTLS / plain).</summary>
    public int Port { get; set; } = 636;

    /// <summary>Gets or sets the transport security mode.</summary>
    public LdapSecurityMode Security { get; set; } = LdapSecurityMode.Ldaps;

    /// <summary>Gets or sets a value indicating whether the server certificate is validated (strongly recommended).</summary>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>Gets or sets the operation timeout in seconds (1–120).</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Gets or sets the base distinguished name, for example <c>DC=corp,DC=example,DC=com</c>.</summary>
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>Gets or sets the distinguished name (or UPN) of the service account used for searches. Anonymous binds are not supported.</summary>
    public string BindDn { get; set; } = string.Empty;

    /// <summary>Gets or sets the NetBIOS domain name accepted as prefix in <c>DOMAIN\user</c> logins (optional).</summary>
    public string? NetBiosDomain { get; set; }

    // ----- Users -----

    /// <summary>Gets or sets the search base for users (defaults to <see cref="BaseDn"/>).</summary>
    public string? UserSearchBase { get; set; }

    /// <summary>Gets or sets the LDAP filter selecting user objects.</summary>
    public string UserFilter { get; set; } = "(&(objectCategory=person)(objectClass=user))";

    /// <summary>Gets or sets the attribute holding the login name.</summary>
    public string UserNameAttribute { get; set; } = "sAMAccountName";

    /// <summary>Gets or sets the attribute holding the user principal name (alternative login, optional).</summary>
    public string? UpnAttribute { get; set; } = "userPrincipalName";

    /// <summary>Gets or sets the display name attribute.</summary>
    public string DisplayNameAttribute { get; set; } = "displayName";

    /// <summary>Gets or sets the first name attribute.</summary>
    public string FirstNameAttribute { get; set; } = "givenName";

    /// <summary>Gets or sets the last name attribute.</summary>
    public string LastNameAttribute { get; set; } = "sn";

    /// <summary>Gets or sets the e-mail attribute.</summary>
    public string EmailAttribute { get; set; } = "mail";

    /// <summary>Gets or sets the phone attribute.</summary>
    public string PhoneAttribute { get; set; } = "telephoneNumber";

    /// <summary>Gets or sets the job title attribute.</summary>
    public string TitleAttribute { get; set; } = "title";

    /// <summary>Gets or sets the department attribute.</summary>
    public string DepartmentAttribute { get; set; } = "department";

    /// <summary>Gets or sets the stable unique identifier attribute (<c>objectGUID</c> for AD, <c>entryUUID</c> for OpenLDAP).</summary>
    public string ExternalIdAttribute { get; set; } = "objectGUID";

    /// <summary>Gets or sets the group membership attribute on user objects.</summary>
    public string MemberOfAttribute { get; set; } = "memberOf";

    // ----- Groups -----

    /// <summary>Gets or sets the search base for groups (defaults to <see cref="BaseDn"/>).</summary>
    public string? GroupSearchBase { get; set; }

    /// <summary>Gets or sets the LDAP filter selecting group objects.</summary>
    public string GroupFilter { get; set; } = "(objectClass=group)";

    /// <summary>
    /// Gets or sets a value indicating whether nested group memberships are resolved (Active Directory
    /// <c>LDAP_MATCHING_RULE_IN_CHAIN</c>). Slower on large directories.
    /// </summary>
    public bool ResolveNestedGroups { get; set; }

    // ----- Provisioning & synchronisation -----

    /// <summary>Gets or sets a value indicating whether unknown directory users are created on their first sign-in.</summary>
    public bool JitProvisioning { get; set; } = true;

    /// <summary>Gets or sets the role name granted to newly provisioned users (optional, for example <c>user</c>).</summary>
    public string? DefaultRoleName { get; set; } = "user";

    /// <summary>Gets or sets a value indicating whether the background synchronisation runs.</summary>
    public bool SyncEnabled { get; set; }

    /// <summary>Gets or sets the synchronisation interval in minutes (15–10080).</summary>
    public int SyncIntervalMinutes { get; set; } = 60;

    /// <summary>Gets or sets a value indicating whether all directory users matching the filter are imported (otherwise only existing ones are refreshed).</summary>
    public bool SyncImportsNewUsers { get; set; }

    /// <summary>Gets or sets a value indicating whether directory users missing from the directory are deactivated.</summary>
    public bool DeactivateMissingUsers { get; set; } = true;

    /// <summary>Gets or sets the maximum number of directory entries read per synchronisation (bounded, 1–100000).</summary>
    public int MaxSyncEntries { get; set; } = 10_000;

    // ----- Windows integrated authentication -----

    /// <summary>Gets or sets a value indicating whether Windows integrated sign-in (Negotiate / Kerberos / NTLM) is offered.</summary>
    public bool WindowsAuthEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether unknown Windows users are provisioned on first sign-in (looked up in the
    /// directory when LDAP is enabled, otherwise created as Windows accounts).
    /// </summary>
    public bool WindowsAutoProvision { get; set; } = true;

    /// <summary>Gets the effective user search base.</summary>
    public string EffectiveUserSearchBase => string.IsNullOrWhiteSpace(UserSearchBase) ? BaseDn : UserSearchBase!;

    /// <summary>Gets the effective group search base.</summary>
    public string EffectiveGroupSearchBase => string.IsNullOrWhiteSpace(GroupSearchBase) ? BaseDn : GroupSearchBase!;

    /// <summary>Gets a value indicating whether the connection settings are complete enough to connect.</summary>
    public bool IsConnectionConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(BaseDn) && !string.IsNullOrWhiteSpace(BindDn);

    /// <summary>
    /// Checks whether the connection target differs from another settings object. When the target changes, the stored
    /// bind password must not be reused (it would be sent to a different server).
    /// </summary>
    /// <param name="other">The settings to compare with.</param>
    /// <returns><see langword="true"/> when host, port, security, certificate validation or bind account differ.</returns>
    public bool HasDifferentConnectionTarget(DirectorySettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return !string.Equals(Host.Trim(), other.Host.Trim(), StringComparison.OrdinalIgnoreCase)
            || Port != other.Port
            || Security != other.Security
            || ValidateCertificate != other.ValidateCertificate
            || !string.Equals(BindDn.Trim(), other.BindDn.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
