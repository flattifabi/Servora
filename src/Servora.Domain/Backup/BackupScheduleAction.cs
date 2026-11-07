namespace Servora.Domain.Backup;

/// <summary>What the backup scheduler should do at a given moment.</summary>
public enum BackupScheduleAction
{
    /// <summary>Nothing is due; wait for the next occurrence.</summary>
    Idle = 0,

    /// <summary>A scheduled occurrence is due (on time or as catch-up after downtime) and must run now.</summary>
    RunNow = 1,

    /// <summary>An occurrence was missed while the service was down and catch-up is disabled; mark it handled.</summary>
    SkipMissed = 2,
}
