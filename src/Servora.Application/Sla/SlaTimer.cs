using Servora.Domain.Sla;

namespace Servora.Application.Sla;

/// <summary>Timer calculations and state transitions of <see cref="TaskSla"/> records.</summary>
public static class SlaTimer
{
    /// <summary>Gets the elapsed schedule seconds of a timer at a point in time.</summary>
    /// <param name="sla">The timer.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="now">The current UTC time.</param>
    /// <returns>The elapsed seconds.</returns>
    public static long ElapsedSeconds(TaskSla sla, SlaSchedule schedule, DateTimeOffset now) =>
        sla.ElapsedSeconds + (sla.Stage == SlaStage.InProgress && sla.ActiveSince is { } since ? (long)schedule.Elapsed(since, now).TotalSeconds : 0);

    /// <summary>Gets the elapsed time in percent of the target.</summary>
    /// <param name="sla">The timer.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="targetMinutes">The target in minutes.</param>
    /// <param name="now">The current UTC time.</param>
    /// <returns>The percentage (can exceed 100).</returns>
    public static double PercentElapsed(TaskSla sla, SlaSchedule schedule, int targetMinutes, DateTimeOffset now) =>
        targetMinutes <= 0 ? 0 : Math.Round(ElapsedSeconds(sla, schedule, now) * 100d / (targetMinutes * 60d), 1);

    /// <summary>Gets the display stage: InProgress, Paused, Achieved, Breached or Cancelled.</summary>
    /// <param name="sla">The timer.</param>
    /// <returns>The stage name.</returns>
    public static string StageName(TaskSla sla) => sla.Stage switch
    {
        SlaStage.Cancelled => "Cancelled",
        SlaStage.Completed => sla.HasBreached ? "Breached" : "Achieved",
        SlaStage.Paused => "Paused",
        _ => sla.HasBreached ? "Breached" : "InProgress",
    };

    /// <summary>Starts a new timer.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="tableName">The table.</param>
    /// <param name="recordId">The record.</param>
    /// <param name="display">The record display value.</param>
    /// <param name="now">The start time.</param>
    /// <returns>The timer.</returns>
    public static TaskSla Start(SlaDefinition definition, SlaSchedule schedule, string tableName, Guid recordId, string? display, DateTimeOffset now) => new()
    {
        SlaDefinitionId = definition.Id,
        TableName = tableName,
        RecordId = recordId,
        RecordDisplay = display is { Length: > 255 } ? display[..255] : display,
        Stage = SlaStage.InProgress,
        StartedAt = now,
        ActiveSince = now,
        DueAt = schedule.Add(now, TimeSpan.FromMinutes(definition.TargetMinutes)),
    };

    /// <summary>Pauses a running timer.</summary>
    /// <param name="sla">The timer.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="now">The current time.</param>
    public static void Pause(TaskSla sla, SlaSchedule schedule, DateTimeOffset now)
    {
        if (sla.Stage != SlaStage.InProgress)
        {
            return;
        }

        sla.ElapsedSeconds = ElapsedSeconds(sla, schedule, now);
        sla.ActiveSince = null;
        sla.PausedAt = now;
        sla.Stage = SlaStage.Paused;
        sla.DueAt = null;
    }

    /// <summary>Resumes a paused timer and recomputes its due date.</summary>
    /// <param name="sla">The timer.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="targetMinutes">The target in minutes.</param>
    /// <param name="now">The current time.</param>
    public static void Resume(TaskSla sla, SlaSchedule schedule, int targetMinutes, DateTimeOffset now)
    {
        if (sla.Stage != SlaStage.Paused)
        {
            return;
        }

        var remaining = TimeSpan.FromMinutes(targetMinutes) - TimeSpan.FromSeconds(sla.ElapsedSeconds);
        sla.Stage = SlaStage.InProgress;
        sla.ActiveSince = now;
        sla.PausedAt = null;
        sla.DueAt = remaining > TimeSpan.Zero ? schedule.Add(now, remaining) : now;
    }

    /// <summary>Completes (stop condition) or cancels a timer.</summary>
    /// <param name="sla">The timer.</param>
    /// <param name="schedule">The schedule.</param>
    /// <param name="targetMinutes">The target in minutes.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancelled">Whether the timer is cancelled instead of completed.</param>
    public static void Stop(TaskSla sla, SlaSchedule schedule, int targetMinutes, DateTimeOffset now, bool cancelled)
    {
        if (sla.Stage is SlaStage.Completed or SlaStage.Cancelled)
        {
            return;
        }

        sla.ElapsedSeconds = ElapsedSeconds(sla, schedule, now);
        sla.ActiveSince = null;
        sla.PausedAt = null;
        sla.StoppedAt = now;
        sla.Stage = cancelled ? SlaStage.Cancelled : SlaStage.Completed;
        if (!cancelled && !sla.HasBreached && sla.ElapsedSeconds > targetMinutes * 60L)
        {
            sla.HasBreached = true;
            sla.BreachedAt = now;
        }
    }
}
