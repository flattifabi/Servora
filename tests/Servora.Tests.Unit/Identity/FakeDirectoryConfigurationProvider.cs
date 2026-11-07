using Servora.Application.Identity.Ldap;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.Identity;

/// <summary>Directory configuration provider returning fixed settings, no bind password and no mappings.</summary>
/// <param name="settings">The settings to return.</param>
public sealed class FakeDirectoryConfigurationProvider(DirectorySettings settings) : IDirectoryConfigurationProvider
{
    /// <inheritdoc />
    public Task<DirectorySettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

    /// <inheritdoc />
    public Task<string?> GetBindPasswordAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("bind-secret");

    /// <inheritdoc />
    public Task<IReadOnlyList<DirectoryGroupMapping>> GetActiveMappingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DirectoryGroupMapping>>([]);
}
