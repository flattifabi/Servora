using Servora.Domain.Backup;

namespace Servora.Tests.Unit.Backup;

/// <summary>Acceptance: schedule calculation (weekly at 02:00 local, DST changes, monthly clamping).</summary>
public sealed class BackupScheduleCalculatorTests
{
    private static readonly TimeZoneInfo s_berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    [Fact]
    public void Weekly_At0200Local_ReturnsNextSundayInLocalTime()
    {
        var schedule = Weekly(DayOfWeek.Sunday);

        // Saturday 7 Nov 2026, 10:00 UTC (CET = UTC+1) → Sunday 8 Nov 02:00 CET = 01:00 UTC.
        var next = BackupScheduleCalculator.GetNextOccurrence(schedule, s_berlin, new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 11, 8, 1, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Weekly_SummerTime_UsesSummerOffset()
    {
        var next = BackupScheduleCalculator.GetNextOccurrence(Weekly(DayOfWeek.Sunday), s_berlin, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        // Sunday 5 Jul 2026 02:00 CEST (UTC+2) = 00:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void SpringForward_NonExistentTime_RunsAfterTheGap()
    {
        // 29 Mar 2026 (Sunday): clocks jump from 02:00 to 03:00 in Berlin; 02:00 does not exist.
        var next = BackupScheduleCalculator.GetNextOccurrence(Weekly(DayOfWeek.Sunday), s_berlin, new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

        // Runs at 03:00 CEST = 01:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void SpringForward_HalfHourInsideGap_IsShiftedByTheGap()
    {
        var schedule = Weekly(DayOfWeek.Sunday);
        schedule.TimeOfDay = new TimeOnly(2, 30);

        var next = BackupScheduleCalculator.GetNextOccurrence(schedule, s_berlin, new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

        // 02:30 does not exist → 03:30 CEST = 01:30 UTC.
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void FallBack_AmbiguousTime_RunsOnce_AtTheFirstOccurrence()
    {
        // 25 Oct 2026 (Sunday): clocks go back from 03:00 CEST to 02:00 CET; 02:00–03:00 happens twice.
        var schedule = Weekly(DayOfWeek.Sunday);
        schedule.TimeOfDay = new TimeOnly(2, 30);
        var first = BackupScheduleCalculator.GetNextOccurrence(schedule, s_berlin, new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

        // First 02:30 is CEST (UTC+2) = 00:30 UTC.
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), first);

        // The second 02:30 (CET, 01:30 UTC) is not another occurrence: the next one is a week later.
        var second = BackupScheduleCalculator.GetNextOccurrence(schedule, s_berlin, first!.Value);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.Zero), second);
    }

    [Fact]
    public void Daily_AcrossDstChange_KeepsLocalWallClockTime()
    {
        var schedule = new BackupSchedule { Enabled = true, Frequency = BackupFrequency.Daily, TimeOfDay = new TimeOnly(23, 15) };
        var cursor = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
        var runs = new List<DateTimeOffset>();
        for (var i = 0; i < 3; i++)
        {
            cursor = BackupScheduleCalculator.GetNextOccurrence(schedule, s_berlin, cursor)!.Value;
            runs.Add(cursor);
        }

        Assert.All(runs, r => Assert.Equal(new TimeOnly(23, 15), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(r, s_berlin).DateTime)));
        Assert.Equal(TimeSpan.FromHours(25), runs[1] - runs[0]);
    }

    [Fact]
    public void Weekly_MultipleWeekdays_PicksTheNearest()
    {
        var schedule = Weekly(DayOfWeek.Monday, DayOfWeek.Thursday);
        schedule.TimeOfDay = new TimeOnly(18, 0);

        var next = BackupScheduleCalculator.GetNextOccurrence(schedule, TimeZoneInfo.Utc, new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Monthly_Day31_IsClampedToTheLastDayOfShortMonths()
    {
        var schedule = new BackupSchedule { Enabled = true, Frequency = BackupFrequency.Monthly, DayOfMonth = 31, TimeOfDay = new TimeOnly(1, 0) };

        var next = BackupScheduleCalculator.GetNextOccurrence(schedule, TimeZoneInfo.Utc, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var afterThat = BackupScheduleCalculator.GetNextOccurrence(schedule, TimeZoneInfo.Utc, next!.Value);

        Assert.Equal(new DateTimeOffset(2026, 2, 28, 1, 0, 0, TimeSpan.Zero), next);
        Assert.Equal(new DateTimeOffset(2026, 3, 31, 1, 0, 0, TimeSpan.Zero), afterThat);
    }

    [Fact]
    public void PreviousOccurrence_ReturnsLatestAtOrBefore()
    {
        var schedule = Weekly(DayOfWeek.Sunday);
        var now = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 11, 8, 1, 0, 0, TimeSpan.Zero), BackupScheduleCalculator.GetPreviousOccurrence(schedule, s_berlin, now));
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 1, 0, 0, TimeSpan.Zero), BackupScheduleCalculator.GetPreviousOccurrence(schedule, s_berlin, new DateTimeOffset(2026, 11, 8, 1, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Validate_RejectsInvalidSettings()
    {
        var schedule = new BackupSchedule { Frequency = BackupFrequency.Weekly, Weekdays = [], DayOfMonth = 40, KeepLastScheduled = -1, TimeZoneId = "Not/AZone" };

        var errors = schedule.Validate();

        Assert.Contains(errors, e => e.Contains("weekday", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("day of month", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Retention", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("time zone", StringComparison.Ordinal));
        Assert.Empty(new BackupSchedule().Validate());
    }

    private static BackupSchedule Weekly(params DayOfWeek[] days) =>
        new() { Enabled = true, Frequency = BackupFrequency.Weekly, Weekdays = [.. days], TimeOfDay = new TimeOnly(2, 0) };
}
