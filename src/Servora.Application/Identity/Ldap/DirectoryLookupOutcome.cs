namespace Servora.Application.Identity.Ldap;

/// <summary>Outcome of a directory user lookup or authentication.</summary>
public enum DirectoryLookupOutcome
{
    /// <summary>The user was found (and, for authentication, the password was accepted).</summary>
    Success = 0,

    /// <summary>No (or more than one) matching user object exists.</summary>
    UserNotFound = 1,

    /// <summary>The user exists but the password was rejected.</summary>
    InvalidPassword = 2,

    /// <summary>The directory could not be reached, the TLS handshake failed or the service bind failed.</summary>
    Unavailable = 3,

    /// <summary>The configuration is incomplete or invalid.</summary>
    ConfigurationError = 4,
}
