using Servora.Domain.Backup;

namespace Servora.Application.Backup;

/// <summary>KPIs and status of the backup subsystem.</summary>
/// <param name="LastSuccessfulBackupAt">When the newest valid backup was created.</param>
/// <param name="LastSuccessfulBackupKind">The kind of the newest valid backup.</param>
/// <param name="NextRunAt">The next scheduled run (UTC), or <see langword="null"/> when the schedule is disabled.</param>
/// <param name="ScheduleEnabled">Whether scheduled backups are enabled.</param>
/// <param name="BackupCount">The number of archives in the catalog.</param>
/// <param name="TotalSizeBytes">The total size of all archives.</param>
/// <param name="LastFailureAt">When the last backup attempt failed, if the last attempt failed.</param>
/// <param name="LastFailureMessage">The failure message of the last failed attempt.</param>
/// <param name="LastRestore">The outcome of the last restore, if any.</param>
/// <param name="BusyOperation">The backup or restore operation currently running, if any.</param>
/// <param name="Destination">The effective destination folder.</param>
/// <param name="RestartsAutomatically">Whether Servora restarts by itself after a restore request (Windows service).</param>
public sealed record BackupOverview(
    DateTimeOffset? LastSuccessfulBackupAt,
    BackupKind? LastSuccessfulBackupKind,
    DateTimeOffset? NextRunAt,
    bool ScheduleEnabled,
    int BackupCount,
    long TotalSizeBytes,
    DateTimeOffset? LastFailureAt,
    string? LastFailureMessage,
    BackupRestoreStatus? LastRestore,
    string? BusyOperation,
    string Destination,
    bool RestartsAutomatically);
