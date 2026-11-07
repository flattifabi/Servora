namespace Servora.Application.Identity.Ldap;

/// <summary>Transport security of the LDAP connection.</summary>
public enum LdapSecurityMode
{
    /// <summary>LDAP over TLS (LDAPS, usually port 636). Recommended.</summary>
    Ldaps = 0,

    /// <summary>Plain LDAP upgraded with the StartTLS extended operation (usually port 389). Fails if the upgrade fails.</summary>
    StartTls = 1,

    /// <summary>Unencrypted LDAP. Credentials travel in clear text – only for isolated lab environments.</summary>
    None = 2,
}
