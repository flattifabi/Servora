using Servora.Application.Common;

namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Administration of LDAP / Active Directory integration and Windows SSO (<c>/admin/directory</c>). Every method
/// requires <c>admin.directory.manage</c>. The bind password is write-only.
/// </summary>
public interface IDirectoryAdminService
{
    /// <summary>Gets the saved settings and status information.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The overview.</returns>
    Task<Result<DirectoryOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and saves the settings. When the connection target (host, port, security, certificate validation or
    /// bind account) changes, a new bind password is required so a stored password is never sent to another server.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="newBindPassword">A new bind password, or <see langword="null"/> to keep the stored one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SaveSettingsAsync(DirectorySettings settings, string? newBindPassword, CancellationToken cancellationToken = default);

    /// <summary>Tests the connection with (possibly unsaved) settings.</summary>
    /// <param name="settings">The settings to test.</param>
    /// <param name="newBindPassword">A bind password typed in the form, or <see langword="null"/> to use the stored one (only for an unchanged target).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The test result.</returns>
    Task<Result<DirectoryTestResult>> TestConnectionAsync(DirectorySettings settings, string? newBindPassword, CancellationToken cancellationToken = default);

    /// <summary>Looks up a user with (possibly unsaved) settings and previews the mapped roles and groups.</summary>
    /// <param name="settings">The settings to use.</param>
    /// <param name="newBindPassword">A bind password typed in the form, or <see langword="null"/>.</param>
    /// <param name="loginName">The login name to look up.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview.</returns>
    Task<Result<DirectoryUserPreview>> TestUserLookupAsync(DirectorySettings settings, string? newBindPassword, string loginName, CancellationToken cancellationToken = default);

    /// <summary>Lists the group mappings.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mappings.</returns>
    Task<Result<IReadOnlyList<DirectoryMappingItem>>> ListMappingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a group mapping. The mapped role's permissions must be held by the actor.</summary>
    /// <param name="model">The mapping.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mapping identifier.</returns>
    Task<Result<Guid>> SaveMappingAsync(DirectoryMappingEdit model, CancellationToken cancellationToken = default);

    /// <summary>Deletes a group mapping. Memberships created by it are removed at the next sign-in or synchronisation.</summary>
    /// <param name="id">The mapping identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteMappingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Runs a synchronisation immediately.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run summary.</returns>
    Task<Result<DirectorySyncSummary>> SyncNowAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets recent synchronisation runs.</summary>
    /// <param name="take">The maximum number of runs (1–100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The runs, newest first.</returns>
    Task<Result<IReadOnlyList<DirectorySyncSummary>>> GetSyncHistoryAsync(int take = 20, CancellationToken cancellationToken = default);
}
