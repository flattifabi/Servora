using Servora.Application.Identity.Ldap;

namespace Servora.Tests.Unit.Identity;

/// <summary>Directory client answering user lookups with a fixed result and counting the calls.</summary>
/// <param name="lookup">The lookup result returned by <see cref="FindUserAsync"/>.</param>
public sealed class FakeDirectoryClient(DirectoryLookupResult lookup) : IDirectoryClient
{
    /// <summary>Gets the number of user lookups performed.</summary>
    public int Lookups { get; private set; }

    /// <inheritdoc />
    public Task<DirectoryTestResult> TestConnectionAsync(DirectorySettings settings, string? bindPassword, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by these tests.");

    /// <inheritdoc />
    public Task<DirectoryLookupResult> FindUserAsync(DirectorySettings settings, string? bindPassword, DirectoryLoginName login, CancellationToken cancellationToken = default)
    {
        Lookups++;
        return Task.FromResult(lookup);
    }

    /// <inheritdoc />
    public Task<DirectoryLookupResult> AuthenticateAsync(DirectorySettings settings, string? bindPassword, DirectoryLoginName login, string password, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by these tests.");

    /// <inheritdoc />
    public Task<DirectorySearchResult> SearchUsersAsync(DirectorySettings settings, string? bindPassword, int maxEntries, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by these tests.");
}
