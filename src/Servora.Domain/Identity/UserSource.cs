namespace Servora.Domain.Identity;

/// <summary>Identifies where a user account is mastered and how it authenticates.</summary>
public enum UserSource
{
    /// <summary>Local account with a password hash stored by Servora.</summary>
    Local = 0,

    /// <summary>Account synchronised from / authenticated against LDAP or Active Directory.</summary>
    Directory = 1,

    /// <summary>Account provisioned on first Windows integrated (Negotiate / Kerberos / NTLM) sign-in.</summary>
    Windows = 2,

    /// <summary>Technical identity used by integrations, API keys or AI agents. Cannot sign in interactively.</summary>
    Service = 3,
}
