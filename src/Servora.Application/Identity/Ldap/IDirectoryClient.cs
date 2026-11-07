namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Low-level LDAP / Active Directory operations, implemented in the infrastructure layer with
/// <c>System.DirectoryServices.Protocols</c>. All operations use the service account for searches, never bind
/// anonymously, never downgrade the configured transport security and are bounded by the configured timeout.
/// </summary>
public interface IDirectoryClient
{
    /// <summary>Connects, secures the connection and binds with the service account.</summary>
    /// <param name="settings">The settings to test (may be unsaved).</param>
    /// <param name="bindPassword">The service account password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The test result with step details.</returns>
    Task<DirectoryTestResult> TestConnectionAsync(DirectorySettings settings, string? bindPassword, CancellationToken cancellationToken = default);

    /// <summary>Finds a single user by login name (exactly one match is required).</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="bindPassword">The service account password.</param>
    /// <param name="login">The parsed login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lookup result.</returns>
    Task<DirectoryLookupResult> FindUserAsync(DirectorySettings settings, string? bindPassword, DirectoryLoginName login, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the user with the service account and verifies the password by binding with the user's distinguished
    /// name. Empty passwords are always rejected (an empty simple bind would be an unauthenticated bind).
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="bindPassword">The service account password.</param>
    /// <param name="login">The parsed login name.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result.</returns>
    Task<DirectoryLookupResult> AuthenticateAsync(DirectorySettings settings, string? bindPassword, DirectoryLoginName login, string password, CancellationToken cancellationToken = default);

    /// <summary>Reads all users matching the user filter with paged searches, bounded by <paramref name="maxEntries"/>.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="bindPassword">The service account password.</param>
    /// <param name="maxEntries">The maximum number of entries to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search result.</returns>
    Task<DirectorySearchResult> SearchUsersAsync(DirectorySettings settings, string? bindPassword, int maxEntries, CancellationToken cancellationToken = default);
}
