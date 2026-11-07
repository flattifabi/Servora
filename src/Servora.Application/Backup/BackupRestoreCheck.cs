namespace Servora.Application.Backup;

/// <summary>Result of <see cref="IBackupService.CheckRestoreAsync"/>.</summary>
/// <param name="Backup">The backup.</param>
/// <param name="CanRestore">Whether the backup can be restored into this installation.</param>
/// <param name="Problems">User-safe reasons why it cannot be restored.</param>
/// <param name="RequiresPassword">Whether the archive is encrypted.</param>
/// <param name="StoredPasswordWorks">Whether the configured backup password decrypts the archive.</param>
/// <param name="RestartsAutomatically">Whether Servora restarts by itself to perform the restore.</param>
public sealed record BackupRestoreCheck(
    BackupInfo Backup,
    bool CanRestore,
    IReadOnlyList<string> Problems,
    bool RequiresPassword,
    bool StoredPasswordWorks,
    bool RestartsAutomatically);
