namespace Servora.Application.Identity.Ldap;

/// <summary>Result of <see cref="IDirectoryClient.SearchUsersAsync"/>.</summary>
/// <param name="Succeeded">Whether the search completed.</param>
/// <param name="Users">The user entries read (bounded).</param>
/// <param name="Truncated">Whether more entries exist than the configured maximum.</param>
/// <param name="Message">A technical, secret-free error message on failure.</param>
public sealed record DirectorySearchResult(bool Succeeded, IReadOnlyList<DirectoryUserEntry> Users, bool Truncated, string? Message = null)
{
    /// <summary>Creates a failed result.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The result.</returns>
    public static DirectorySearchResult Failed(string message) => new(false, [], false, message);
}
