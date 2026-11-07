using System.Globalization;
using Servora.Domain.Sla;

namespace Servora.Application.Sla;

/// <summary>
/// Working-time calendar of an SLA definition: 24×7 or business hours (weekdays and daily window in a time zone,
/// daylight saving aware). Measures elapsed schedule time and computes due dates. Calculations are bounded to ten years.
/// </summary>
public sealed class SlaSchedule
{
    private const int MaxDays = 3660;

    private SlaSchedule(SlaScheduleType type, TimeZoneInfo timeZone, IReadOnlySet<DayOfWeek> days, int startMinute, int endMinute)
    {
        Type = days.Count == 0 || endMinute <= startMinute ? SlaScheduleType.TwentyFourSeven : type;
        TimeZone = timeZone;
        Days = days;
        StartMinute = startMinute;
        EndMinute = endMinute;
    }

    /// <summary>Gets the effective schedule type.</summary>
    public SlaScheduleType Type { get; }

    /// <summary>Gets the time zone of the business hours.</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>Gets the business days.</summary>
    public IReadOnlySet<DayOfWeek> Days { get; }

    /// <summary>Gets the start of the business day in minutes after local midnight.</summary>
    public int StartMinute { get; }

    /// <summary>Gets the end of the business day in minutes after local midnight.</summary>
    public int EndMinute { get; }

    /// <summary>Gets a 24×7 schedule.</summary>
    public static SlaSchedule TwentyFourSeven { get; } = new(SlaScheduleType.TwentyFourSeven, TimeZoneInfo.Utc, new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>()), 0, 1440);

    /// <summary>Creates the schedule of a definition.</summary>
    /// <param name="definition">The SLA definition.</param>
    /// <returns>The schedule.</returns>
    public static SlaSchedule From(SlaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.ScheduleType == SlaScheduleType.TwentyFourSeven
            ? TwentyFourSeven
            : Create(definition.TimeZoneId, ParseDays(definition.BusinessDays), definition.DayStartMinute, definition.DayEndMinute);
    }

    /// <summary>Creates a business-hours schedule.</summary>
    /// <param name="timeZoneId">The IANA or Windows time zone identifier (UTC when unknown).</param>
    /// <param name="days">The business days.</param>
    /// <param name="startMinute">The start of the business day in minutes after midnight.</param>
    /// <param name="endMinute">The end of the business day in minutes after midnight.</param>
    /// <returns>The schedule.</returns>
    public static SlaSchedule Create(string? timeZoneId, IEnumerable<DayOfWeek> days, int startMinute, int endMinute) =>
        new(SlaScheduleType.BusinessHours, ResolveTimeZone(timeZoneId), new HashSet<DayOfWeek>(days), Math.Clamp(startMinute, 0, 1440), Math.Clamp(endMinute, 0, 1440));

    /// <summary>Resolves a time zone identifier.</summary>
    /// <param name="timeZoneId">The identifier.</param>
    /// <returns>The time zone, or UTC when the identifier is unknown.</returns>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone) ? zone : TimeZoneInfo.Utc;
    }

    /// <summary>Parses comma separated day numbers (0 = Sunday … 6 = Saturday).</summary>
    /// <param name="text">The list.</param>
    /// <returns>The days.</returns>
    public static IReadOnlyList<DayOfWeek> ParseDays(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 6 ? (DayOfWeek?)n : null)
                .Where(d => d is not null)
                .Select(d => d!.Value)
                .Distinct()
                .ToList();

    /// <summary>Measures the schedule time between two instants.</summary>
    /// <param name="from">The start (UTC).</param>
    /// <param name="to">The end (UTC).</param>
    /// <returns>The elapsed schedule time (zero when <paramref name="to"/> is not after <paramref name="from"/>).</returns>
    public TimeSpan Elapsed(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return TimeSpan.Zero;
        }

        if (Type == SlaScheduleType.TwentyFourSeven)
        {
            return to - from;
        }

        var total = TimeSpan.Zero;
        var day = LocalDate(from).AddDays(-1);
        var last = LocalDate(to).AddDays(1);
        for (var i = 0; day <= last && i < MaxDays; i++, day = day.AddDays(1))
        {
            if (Window(day) is not { } window)
            {
                continue;
            }

            var start = window.Start > from ? window.Start : from;
            var end = window.End < to ? window.End : to;
            if (end > start)
            {
                total += end - start;
            }
        }

        return total;
    }

    /// <summary>Adds schedule time to an instant (for example to compute the due date of a timer).</summary>
    /// <param name="start">The start (UTC).</param>
    /// <param name="duration">The schedule duration.</param>
    /// <returns>The UTC instant at which the duration is consumed.</returns>
    public DateTimeOffset Add(DateTimeOffset start, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return start;
        }

        if (Type == SlaScheduleType.TwentyFourSeven)
        {
            return start + duration;
        }

        var remaining = duration;
        var day = LocalDate(start).AddDays(-1);
        for (var i = 0; i < MaxDays; i++, day = day.AddDays(1))
        {
            if (Window(day) is not { } window || window.End <= start)
            {
                continue;
            }

            var segmentStart = window.Start > start ? window.Start : start;
            var available = window.End - segmentStart;
            if (remaining <= available)
            {
                return segmentStart + remaining;
            }

            remaining -= available;
        }

        return start + duration;
    }

    private DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);

    private (DateTimeOffset Start, DateTimeOffset End)? Window(DateOnly day)
    {
        if (!Days.Contains(day.DayOfWeek))
        {
            return null;
        }

        var start = ToUtc(day.ToDateTime(TimeOnly.MinValue).AddMinutes(StartMinute));
        var end = ToUtc(day.ToDateTime(TimeOnly.MinValue).AddMinutes(EndMinute));
        return end > start ? (start, end) : null;
    }

    private DateTimeOffset ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // Local times inside a daylight saving gap do not exist; move them forward to the first valid instant.
        for (var i = 0; i < 4 && TimeZone.IsInvalidTime(unspecified); i++)
        {
            unspecified = unspecified.AddMinutes(30);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone), TimeSpan.Zero);
    }
}
