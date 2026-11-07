using Servora.Application.Common;

namespace Servora.Application.Observability;

/// <summary>
/// Log viewer and debug switch use cases. Reading requires <c>admin.logs.read</c>; changing levels or retention requires
/// <c>admin.logs.manage</c>. Level changes are applied without restart, persisted and audited.
/// </summary>
public interface ILoggingAdminService
{
    /// <summary>Gets levels, statistics, retention and files.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The overview.</returns>
    Task<Result<LoggingOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Queries the in-memory log buffer (newest first).</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries.</returns>
    Task<Result<IReadOnlyList<LogEntry>>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Exports the filtered buffer as JSON lines.</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The UTF-8 JSON-lines document.</returns>
    Task<Result<byte[]>> ExportAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Opens a log file for download.</summary>
    /// <param name="fileName">The plain file name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stream.</returns>
    Task<Result<Stream>> OpenFileAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Changes the runtime log levels (debug switch).</summary>
    /// <param name="update">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateLevelsAsync(LogLevelUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Removes every runtime override (back to configuration defaults).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ResetLevelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes log file retention and size cap and deletes expired files.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateRetentionAsync(LogRetentionSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Loads and applies persisted levels and retention (start-up; system actor).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when applied.</returns>
    Task RestorePersistedAsync(CancellationToken cancellationToken = default);

    /// <summary>Reverts the levels when the auto-revert time has passed (background; system actor).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the levels were reverted.</returns>
    Task<bool> RevertIfDueAsync(CancellationToken cancellationToken = default);
}
