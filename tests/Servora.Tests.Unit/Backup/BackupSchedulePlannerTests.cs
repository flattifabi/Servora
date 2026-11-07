using Servora.Domain.Backup;

namespace Servora.Tests.Unit.Backup;

/// <summary>Acceptance: missed run catch-up and once-per-occurrence semantics of the scheduler decision.</summary>
public sealed class BackupSchedulePlannerTests
{
    private static readonly TimeZoneInfo s_berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly DateTimeOffset s_sundayRun = new(2026, 11, 8, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_previousSundayRun = new(2026, 11, 1, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_baseline = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DueOccurrence_RunsOnTime()
    {
        var decision = Evaluate(lastHandled: s_previousSundayRun, now: s_sundayRun.AddSeconds(30));

        Assert.Equal(BackupScheduleAction.RunNow, decision.Action);
        Assert.Equal(s_sundayRun, decision.Occurrence);
        Assert.False(decision.IsCatchUp);
        Assert.Equal(s_sundayRun.AddDays(7), decision.NextOccurrence);
    }

    [Fact]
    public void HandledOccurrence_DoesNotRunTwice()
    {
        var decision = Evaluate(lastHandled: s_sundayRun, now: s_sundayRun.AddMinutes(1));

        Assert.Equal(BackupScheduleAction.Idle, decision.Action);
        Assert.Equal(s_sundayRun.AddDays(7), decision.NextOccurrence);
    }

    [Fact]
    public void MissedRun_IsCaughtUp_AfterDowntime()
    {
        // Servora was down on Sunday night and starts on Monday 10:00.
        var decision = Evaluate(lastHandled: s_previousSundayRun, now: new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(BackupScheduleAction.RunNow, decision.Action);
        Assert.Equal(s_sundayRun, decision.Occurrence);
        Assert.True(decision.IsCatchUp);
    }

    [Fact]
    public void MissedRun_IsSkipped_WhenCatchUpIsDisabled()
    {
        var decision = Evaluate(lastHandled: s_previousSundayRun, now: new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero), catchUp: false);

        Assert.Equal(BackupScheduleAction.SkipMissed, decision.Action);
        Assert.Equal(s_sundayRun, decision.Occurrence);
    }

    [Fact]
    public void SeveralMissedRuns_CatchUpOnlyTheLatest()
    {
        var decision = Evaluate(lastHandled: s_previousSundayRun.AddDays(-14), now: new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(BackupScheduleAction.RunNow, decision.Action);
        Assert.Equal(s_sundayRun, decision.Occurrence);
    }

    [Fact]
    public void ChangingTheSchedule_DoesNotTriggerAnImmediateCatchUp()
    {
        // The schedule was saved on Monday, after Sunday's occurrence.
        var baseline = new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
        var decision = BackupSchedulePlanner.Evaluate(Schedule(true), s_berlin, null, baseline, baseline.AddMinutes(1));

        Assert.Equal(BackupScheduleAction.Idle, decision.Action);
    }

    [Fact]
    public void DisabledSchedule_IsIdle_WithoutNextRun()
    {
        var schedule = Schedule(true);
        schedule.Enabled = false;

        var decision = BackupSchedulePlanner.Evaluate(schedule, s_berlin, null, s_baseline, s_sundayRun);

        Assert.Equal(BackupScheduleAction.Idle, decision.Action);
        Assert.Null(decision.NextOccurrence);
    }

    private static BackupScheduleDecision Evaluate(DateTimeOffset? lastHandled, DateTimeOffset now, bool catchUp = true) =>
        BackupSchedulePlanner.Evaluate(Schedule(catchUp), s_berlin, lastHandled, s_baseline, now);

    private static BackupSchedule Schedule(bool catchUp) => new()
    {
        Enabled = true,
        Frequency = BackupFrequency.Weekly,
        Weekdays = [DayOfWeek.Sunday],
        TimeOfDay = new TimeOnly(2, 0),
        CatchUpMissedRuns = catchUp,
    };
}
