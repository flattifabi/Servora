using Servora.Application.Sla;
using Servora.Domain.Sla;

namespace Servora.Tests.Unit.Platform;

/// <summary>Tests for SLA time calculation (24×7 and business hours, time zones, daylight saving).</summary>
public sealed class SlaScheduleTests
{
    private static readonly DayOfWeek[] s_weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void TwentyFourSeven_CountsWallClockTime()
    {
        var schedule = SlaSchedule.TwentyFourSeven;
        Assert.Equal(TimeSpan.FromHours(30), schedule.Elapsed(Utc(2026, 3, 6, 18), Utc(2026, 3, 8, 0)));
        Assert.Equal(Utc(2026, 3, 7, 2), schedule.Add(Utc(2026, 3, 6, 18), TimeSpan.FromHours(8)));
        Assert.Equal(TimeSpan.Zero, schedule.Elapsed(Utc(2026, 3, 8, 0), Utc(2026, 3, 6, 0)));
    }

    [Fact]
    public void BusinessHours_SkipNightsAndWeekends()
    {
        // 2026-03-06 is a Friday.
        var schedule = SlaSchedule.Create("UTC", s_weekdays, 8 * 60, 17 * 60);

        // Friday 16:00 + 4 business hours = Monday 11:00.
        Assert.Equal(Utc(2026, 3, 9, 11), schedule.Add(Utc(2026, 3, 6, 16), TimeSpan.FromHours(4)));

        // Started on Saturday: the clock starts Monday 08:00.
        Assert.Equal(Utc(2026, 3, 9, 10), schedule.Add(Utc(2026, 3, 7, 12), TimeSpan.FromHours(2)));

        // Friday 16:00 .. Monday 11:00 = 1 h + 3 h.
        Assert.Equal(TimeSpan.FromHours(4), schedule.Elapsed(Utc(2026, 3, 6, 16), Utc(2026, 3, 9, 11)));

        // A full week contains 45 business hours.
        Assert.Equal(TimeSpan.FromHours(45), schedule.Elapsed(Utc(2026, 3, 9, 0), Utc(2026, 3, 16, 0)));

        // Before business hours: nothing elapses.
        Assert.Equal(TimeSpan.Zero, schedule.Elapsed(Utc(2026, 3, 9, 5), Utc(2026, 3, 9, 7, 59)));
    }

    [Fact]
    public void BusinessHours_RespectTimeZone()
    {
        var schedule = SlaSchedule.Create("Europe/Berlin", s_weekdays, 9 * 60, 17 * 60);

        // 2026-01-12 (Monday, CET = UTC+1): 09:00 local = 08:00 UTC; 2 h -> 10:00 UTC.
        Assert.Equal(Utc(2026, 1, 12, 10), schedule.Add(Utc(2026, 1, 12, 6), TimeSpan.FromHours(2)));

        // 16:30 local (15:30 UTC) + 1 h = 30 min today + 30 min next day from 09:00 local (08:00 UTC).
        Assert.Equal(Utc(2026, 1, 13, 8, 30), schedule.Add(Utc(2026, 1, 12, 15, 30), TimeSpan.FromHours(1)));
    }

    [Fact]
    public void BusinessHours_HandleDaylightSavingChange()
    {
        // Europe/Berlin switches to CEST (UTC+2) on 2026-03-29. Friday 16:00 UTC is 17:00 CET (end of day), so one
        // business hour ends on Monday 2026-03-30 at 10:00 CEST = 08:00 UTC.
        var schedule = SlaSchedule.Create("Europe/Berlin", s_weekdays, 9 * 60, 17 * 60);
        Assert.Equal(Utc(2026, 3, 30, 8), schedule.Add(Utc(2026, 3, 27, 16), TimeSpan.FromHours(1)));
        Assert.Equal(TimeSpan.FromHours(16), schedule.Elapsed(Utc(2026, 3, 26, 0), Utc(2026, 3, 28, 0)));
    }

    [Fact]
    public void InvalidSchedules_FallBackTo24x7()
    {
        Assert.Equal(SlaScheduleType.TwentyFourSeven, SlaSchedule.Create("UTC", [], 480, 1020).Type);
        Assert.Equal(SlaScheduleType.TwentyFourSeven, SlaSchedule.Create("UTC", s_weekdays, 1020, 480).Type);
        Assert.Equal(TimeZoneInfo.Utc, SlaSchedule.ResolveTimeZone("Not/AZone"));
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], SlaSchedule.ParseDays("1, 5, 9, x, 1"));
    }

    [Fact]
    public void Timer_PauseResumeAndBreachAccounting()
    {
        var definition = new SlaDefinition { Name = "Resolve", TargetMinutes = 60 };
        var schedule = SlaSchedule.TwentyFourSeven;
        var start = Utc(2026, 3, 9, 8);
        var sla = SlaTimer.Start(definition, schedule, "u_x", Guid.NewGuid(), "X1", start);
        Assert.Equal(start.AddHours(1), sla.DueAt);

        SlaTimer.Pause(sla, schedule, start.AddMinutes(20));
        Assert.Equal(SlaStage.Paused, sla.Stage);
        Assert.Null(sla.DueAt);
        Assert.Equal(20 * 60, SlaTimer.ElapsedSeconds(sla, schedule, start.AddHours(5)));

        SlaTimer.Resume(sla, schedule, definition.TargetMinutes, start.AddHours(2));
        Assert.Equal(start.AddHours(2).AddMinutes(40), sla.DueAt);
        Assert.Equal(50d, SlaTimer.PercentElapsed(sla, schedule, definition.TargetMinutes, start.AddHours(2).AddMinutes(10)));

        SlaTimer.Stop(sla, schedule, definition.TargetMinutes, start.AddHours(3), cancelled: false);
        Assert.Equal(SlaStage.Completed, sla.Stage);
        Assert.True(sla.HasBreached);
        Assert.Equal("Breached", SlaTimer.StageName(sla));
    }
}
