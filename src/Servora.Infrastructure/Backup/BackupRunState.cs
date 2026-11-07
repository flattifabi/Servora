using Servora.Application.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>Runtime state of the backup subsystem persisted in <c>backup.json</c> (survives restores).</summary>
public sealed class BackupRunState
{
    /// <summary>Gets or sets the last scheduled occurrence that was run or skipped.</summary>
    public DateTimeOffset? LastHandledOccurrence { get; set; }

    /// <summary>Gets or sets when the schedule was last changed; earlier occurrences are never run.</summary>
    public DateTimeOffset ScheduleBaseline { get; set; }

    /// <summary>Gets or sets when the last backup attempt started.</summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>Gets or sets when the last backup succeeded.</summary>
    public DateTimeOffset? LastSuccessAt { get; set; }

    /// <summary>Gets or sets the archive created by the last successful backup.</summary>
    public string? LastSuccessId { get; set; }

    /// <summary>Gets or sets when the last backup attempt failed (cleared by the next success).</summary>
    public DateTimeOffset? LastFailureAt { get; set; }

    /// <summary>Gets or sets the user-safe failure message of the last failed attempt.</summary>
    public string? LastFailureMessage { get; set; }

    /// <summary>Gets or sets the outcome of the last restore.</summary>
    public BackupRestoreStatus? LastRestore { get; set; }

    /// <summary>Gets or sets audit events recorded while no database / audit service was available (startup, CLI).</summary>
    public List<PendingBackupAudit> PendingAudits { get; set; } = [];
}
