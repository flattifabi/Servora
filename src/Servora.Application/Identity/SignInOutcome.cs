namespace Servora.Application.Identity;

/// <summary>Outcome classification of a credential validation.</summary>
public enum SignInOutcome
{
    /// <summary>Credentials are valid.</summary>
    Success = 0,

    /// <summary>Unknown user or wrong password (never disclosed separately to the client).</summary>
    InvalidCredentials = 1,

    /// <summary>The account is locked out after too many failed attempts.</summary>
    LockedOut = 2,

    /// <summary>The account is deactivated.</summary>
    Disabled = 3,

    /// <summary>The directory (LDAP / AD) could not be reached.</summary>
    DirectoryUnavailable = 4,
}
