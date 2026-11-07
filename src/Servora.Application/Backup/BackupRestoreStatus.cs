namespace Servora.Application.Backup;

/// <summary>Outcome of the last restore executed at startup.</summary>
/// <param name="At">When the restore finished (UTC).</param>
/// <param name="Succeeded">Whether the restore succeeded.</param>
/// <param name="BackupId">The restored backup.</param>
/// <param name="Message">A user-safe summary.</param>
public sealed record BackupRestoreStatus(DateTimeOffset At, bool Succeeded, string BackupId, string Message);
