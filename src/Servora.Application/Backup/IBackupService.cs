using Servora.Application.Common;

namespace Servora.Application.Backup;

/// <summary>
/// Administrative backup and restore use cases behind <c>/admin/backups</c>. Every method checks the caller's
/// permissions (<c>backup.read</c>, <c>backup.manage</c>, <c>backup.restore</c>) and audits state-changing actions.
/// </summary>
public interface IBackupService
{
    /// <summary>Returns the KPIs and status shown at the top of the backup page.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The overview.</returns>
    Task<Result<BackupOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the backups of the catalog, newest first.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The backups.</returns>
    Task<Result<IReadOnlyList<BackupInfo>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the schedule, retention and encryption settings.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings.</returns>
    Task<Result<BackupSettingsView>> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the schedule, retention and encryption settings.</summary>
    /// <param name="update">The new settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SaveSettingsAsync(BackupSettingsUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Creates a manual backup now.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created backup.</returns>
    Task<Result<BackupInfo>> CreateBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>Verifies the SHA-256 hashes (and the signature of encrypted archives) of a backup.</summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verification result.</returns>
    Task<Result<BackupVerificationResult>> VerifyAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a backup archive.</summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>Imports an uploaded archive. It is validated (manifest and hashes) before it appears in the catalog.</summary>
    /// <param name="content">The uploaded content.</param>
    /// <param name="fileName">The original file name (only used for display and logging).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The imported backup.</returns>
    Task<Result<BackupInfo>> ImportAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Opens a backup archive for download.</summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The download.</returns>
    Task<Result<BackupDownload>> OpenDownloadAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a backup can be restored into this installation.</summary>
    /// <param name="backupId">The backup identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The check result.</returns>
    Task<Result<BackupRestoreCheck>> CheckRestoreAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a restore: verifies the archive, creates a pre-restore safety backup, writes the restore request into
    /// the data directory and restarts Servora, which performs the restore at startup before migrations.
    /// </summary>
    /// <param name="input">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RequestRestoreAsync(BackupRestoreRequestInput input, CancellationToken cancellationToken = default);

    /// <summary>Returns the database schema state (current, applied and pending migrations).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The schema status.</returns>
    Task<Result<DatabaseSchemaStatus>> GetSchemaStatusAsync(CancellationToken cancellationToken = default);
}
