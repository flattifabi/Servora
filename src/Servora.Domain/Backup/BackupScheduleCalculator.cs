namespace Servora.Domain.Backup;

/// <summary>
/// Computes the occurrences of a <see cref="BackupSchedule"/> in its time zone, including daylight saving time
/// transitions: a run time that does not exist on the day clocks spring forward moves forward by the gap (02:30 becomes
/// 03:30), and a run time that occurs twice when clocks fall back runs once, at its first occurrence.
/// </summary>
public static class BackupScheduleCalculator
{
    private const int SearchDays = 400;

    /// <summary>Returns the first occurrence strictly after <paramref name="afterUtc"/>.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="timeZone">The schedule's time zone.</param>
    /// <param name="afterUtc">The reference instant.</param>
    /// <returns>The occurrence in UTC, or <see langword="null"/> when the schedule never fires.</returns>
    public static DateTimeOffset? GetNextOccurrence(BackupSchedule schedule, TimeZoneInfo timeZone, DateTimeOffset afterUtc)
    {
        var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(afterUtc, timeZone).DateTime).AddDays(-1);
        for (var i = 0; i <= SearchDays; i++)
        {
            var date = startDate.AddDays(i);
            if (!Matches(schedule, date))
            {
                continue;
            }

            var occurrence = ResolveOccurrence(date, schedule.TimeOfDay, timeZone);
            if (occurrence > afterUtc)
            {
                return occurrence;
            }
        }

        return null;
    }

    /// <summary>Returns the latest occurrence at or before <paramref name="atOrBeforeUtc"/>.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="timeZone">The schedule's time zone.</param>
    /// <param name="atOrBeforeUtc">The reference instant.</param>
    /// <returns>The occurrence in UTC, or <see langword="null"/> when there is none in the search window.</returns>
    public static DateTimeOffset? GetPreviousOccurrence(BackupSchedule schedule, TimeZoneInfo timeZone, DateTimeOffset atOrBeforeUtc)
    {
        var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(atOrBeforeUtc, timeZone).DateTime).AddDays(1);
        for (var i = 0; i <= SearchDays; i++)
        {
            var date = startDate.AddDays(-i);
            if (!Matches(schedule, date))
            {
                continue;
            }

            var occurrence = ResolveOccurrence(date, schedule.TimeOfDay, timeZone);
            if (occurrence <= atOrBeforeUtc)
            {
                return occurrence;
            }
        }

        return null;
    }

    /// <summary>Checks whether the schedule fires on a local calendar date.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="date">The local date.</param>
    /// <returns><see langword="true"/> when a run is planned on that date.</returns>
    public static bool Matches(BackupSchedule schedule, DateOnly date) => schedule.Frequency switch
    {
        BackupFrequency.Daily => true,
        BackupFrequency.Weekly => schedule.Weekdays.Contains(date.DayOfWeek),
        BackupFrequency.Monthly => date.Day == Math.Min(Math.Clamp(schedule.DayOfMonth, 1, 31), DateTime.DaysInMonth(date.Year, date.Month)),
        _ => false,
    };

    /// <summary>Converts a local date and time of day in a time zone to the UTC instant of the run.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="timeOfDay">The local time of day.</param>
    /// <param name="timeZone">The time zone.</param>
    /// <returns>The instant.</returns>
    public static DateTimeOffset ResolveOccurrence(DateOnly date, TimeOnly timeOfDay, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(timeOfDay, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(local))
        {
            // Spring forward: the wall clock skips this time. Shift by the length of the gap so 02:30 becomes 03:30.
            var before = timeZone.GetUtcOffset(local.AddHours(-12));
            var after = timeZone.GetUtcOffset(local.AddHours(12));
            var gap = after - before;
            var shifted = local + (gap > TimeSpan.Zero ? gap : TimeSpan.FromHours(1));
            return new DateTimeOffset(shifted, timeZone.GetUtcOffset(shifted)).ToUniversalTime();
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            // Fall back: the wall clock shows this time twice. Run once, at the first occurrence (largest offset).
            var offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }
}
