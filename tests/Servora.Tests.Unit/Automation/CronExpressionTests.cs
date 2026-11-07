using Servora.Application.Automation.Scheduling;

namespace Servora.Tests.Unit.Automation;

/// <summary>Tests of the 5-field <see cref="CronExpression"/> parser and occurrence calculation.</summary>
public sealed class CronExpressionTests
{
    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("*/15 * * * *", "2026-11-09T08:07:00Z", "2026-11-09T08:15:00Z")]
    [InlineData("*/15 * * * *", "2026-11-09T08:45:00Z", "2026-11-09T09:00:00Z")]
    [InlineData("0 * * * *", "2026-11-09T23:30:00Z", "2026-11-10T00:00:00Z")]
    [InlineData("0 8 * * 1-5", "2026-11-13T09:00:00Z", "2026-11-16T08:00:00Z")]
    [InlineData("30 6 1 * *", "2026-11-09T00:00:00Z", "2026-12-01T06:30:00Z")]
    [InlineData("0 9 * * MON", "2026-11-07T10:00:00Z", "2026-11-09T09:00:00Z")]
    [InlineData("0 0 1 JAN *", "2026-11-07T10:00:00Z", "2027-01-01T00:00:00Z")]
    [InlineData("0 12 * * 7", "2026-11-07T13:00:00Z", "2026-11-08T12:00:00Z")]
    [InlineData("@daily", "2026-11-07T13:00:00Z", "2026-11-08T00:00:00Z")]
    [InlineData("@hourly", "2026-11-07T13:59:00Z", "2026-11-07T14:00:00Z")]
    [InlineData("0-30/10 14 * * *", "2026-11-07T14:05:00Z", "2026-11-07T14:10:00Z")]
    [InlineData("5,35 * * * *", "2026-11-07T14:06:00Z", "2026-11-07T14:35:00Z")]
    public void GetNextOccurrence_ReturnsExpectedUtcTime(string expression, string after, string expected)
    {
        var cron = CronExpression.Parse(expression);

        var next = cron.GetNextOccurrence(DateTimeOffset.Parse(after));

        Assert.Equal(DateTimeOffset.Parse(expected), next);
    }

    [Fact]
    public void GetNextOccurrence_IsStrictlyAfterReference()
    {
        var cron = CronExpression.Parse("0 8 * * *");

        Assert.Equal(Utc(2026, 11, 10, 8), cron.GetNextOccurrence(Utc(2026, 11, 9, 8)));
    }

    [Fact]
    public void RestrictedDayOfMonthAndWeekday_MatchEither()
    {
        // Vixie semantics: "1st of the month OR Monday". 2026-11-01 is a Sunday, 2026-11-02 a Monday.
        var cron = CronExpression.Parse("0 0 1 * MON");

        Assert.Equal(Utc(2026, 11, 2), cron.GetNextOccurrence(Utc(2026, 11, 1)));
        Assert.Equal(Utc(2026, 11, 9), cron.GetNextOccurrence(Utc(2026, 11, 2)));
    }

    [Fact]
    public void TimeZone_IsAppliedToWallClock()
    {
        Assert.True(CronExpression.TryFindTimeZone("Europe/Berlin", out var berlin));
        var cron = CronExpression.Parse("0 8 * * *");

        // November: CET = UTC+1.
        Assert.Equal(Utc(2026, 11, 9, 7), cron.GetNextOccurrence(Utc(2026, 11, 9), berlin));

        // July: CEST = UTC+2.
        Assert.Equal(Utc(2027, 7, 1, 6), cron.GetNextOccurrence(Utc(2027, 7, 1), berlin));
    }

    [Fact]
    public void DaylightSavingGap_SkipsNonExistentLocalTime()
    {
        Assert.True(CronExpression.TryFindTimeZone("Europe/Berlin", out var berlin));
        var cron = CronExpression.Parse("30 2 * * *");

        // 2027-03-28 02:30 does not exist in Berlin; the next run is 2027-03-29 02:30 CEST.
        Assert.Equal(Utc(2027, 3, 29, 0, 30), cron.GetNextOccurrence(Utc(2027, 3, 27, 12), berlin));
    }

    [Fact]
    public void DaylightSavingOverlap_FiresOnce()
    {
        Assert.True(CronExpression.TryFindTimeZone("Europe/Berlin", out var berlin));
        var cron = CronExpression.Parse("30 2 * * *");

        // 2026-10-25 02:30 happens twice in Berlin; only the first (CEST, 00:30 UTC) fires.
        var occurrences = cron.GetNextOccurrences(Utc(2026, 10, 24, 12), 2, berlin);

        Assert.Equal([Utc(2026, 10, 25, 0, 30), Utc(2026, 10, 26, 1, 30)], occurrences);
    }

    [Fact]
    public void GetNextOccurrences_AreIncreasing()
    {
        var occurrences = CronExpression.Parse("*/20 * * * *").GetNextOccurrences(Utc(2026, 11, 9, 8, 1), 4);

        Assert.Equal([Utc(2026, 11, 9, 8, 20), Utc(2026, 11, 9, 8, 40), Utc(2026, 11, 9, 9), Utc(2026, 11, 9, 9, 20)], occurrences);
    }

    [Fact]
    public void ImpossibleDate_HasNoOccurrence()
    {
        Assert.Null(CronExpression.Parse("0 0 30 2 *").GetNextOccurrence(Utc(2026, 11, 9)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* 24 * * *")]
    [InlineData("* * 0 * *")]
    [InlineData("* * 32 * *")]
    [InlineData("* * * 13 *")]
    [InlineData("* * * * 8")]
    [InlineData("*/0 * * * *")]
    [InlineData("30-10 * * * *")]
    [InlineData("1,,2 * * * *")]
    [InlineData("x * * * *")]
    [InlineData("* * * FOO *")]
    public void TryParse_RejectsInvalidExpressions(string? expression)
    {
        Assert.False(CronExpression.TryParse(expression, out var cron, out var error));
        Assert.Null(cron);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Matches_ChecksWallClockMinute()
    {
        var cron = CronExpression.Parse("15 10 * * TUE");

        Assert.True(cron.Matches(new DateTime(2026, 11, 10, 10, 15, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 11, 10, 10, 16, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 11, 11, 10, 15, 0)));
    }

    [Fact]
    public void TryFindTimeZone_FallsBackToUtc()
    {
        Assert.True(CronExpression.TryFindTimeZone(null, out var empty));
        Assert.Equal(TimeZoneInfo.Utc, empty);
        Assert.False(CronExpression.TryFindTimeZone("Mars/Olympus_Mons", out var unknown));
        Assert.Equal(TimeZoneInfo.Utc, unknown);
    }
}
