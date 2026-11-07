namespace Servora.Domain.Backup;

/// <summary>
/// Decides whether a scheduled backup is due. The scheduler remembers the last occurrence it handled (run or skipped)
/// and the moment the schedule was last changed (the baseline), so changing the schedule never triggers an immediate
/// catch-up run and every occurrence is handled at most once.
/// </summary>
public static class BackupSchedulePlanner
{
    /// <summary>How late an occurrence may be handled and still count as "on time" rather than missed.</summary>
    public static readonly TimeSpan OnTimeTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Evaluates the schedule at <paramref name="nowUtc"/>.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="timeZone">The schedule's time zone.</param>
    /// <param name="lastHandledOccurrenceUtc">The last occurrence that was run or skipped, if any.</param>
    /// <param name="baselineUtc">When the schedule was last changed; occurrences before it are never run.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <returns>The decision.</returns>
    public static BackupScheduleDecision Evaluate(
        BackupSchedule schedule,
        TimeZoneInfo timeZone,
        DateTimeOffset? lastHandledOccurrenceUtc,
        DateTimeOffset baselineUtc,
        DateTimeOffset nowUtc)
    {
        if (!schedule.Enabled)
        {
            return new BackupScheduleDecision(BackupScheduleAction.Idle, null, null);
        }

        var next = BackupScheduleCalculator.GetNextOccurrence(schedule, timeZone, nowUtc);
        var previous = BackupScheduleCalculator.GetPreviousOccurrence(schedule, timeZone, nowUtc);
        var reference = lastHandledOccurrenceUtc is { } handled && handled > baselineUtc ? handled : baselineUtc;

        if (previous is not { } occurrence || occurrence <= reference)
        {
            return new BackupScheduleDecision(BackupScheduleAction.Idle, null, next);
        }

        if (nowUtc - occurrence <= OnTimeTolerance)
        {
            return new BackupScheduleDecision(BackupScheduleAction.RunNow, occurrence, next);
        }

        return schedule.CatchUpMissedRuns
            ? new BackupScheduleDecision(BackupScheduleAction.RunNow, occurrence, next, IsCatchUp: true)
            : new BackupScheduleDecision(BackupScheduleAction.SkipMissed, occurrence, next);
    }
}
