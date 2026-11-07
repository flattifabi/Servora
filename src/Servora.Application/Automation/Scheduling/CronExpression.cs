using System.Globalization;

namespace Servora.Application.Automation.Scheduling;

/// <summary>
/// A standard 5-field cron expression (<c>minute hour day-of-month month day-of-week</c>) with lists (<c>1,5</c>),
/// ranges (<c>1-5</c>), steps (<c>*/15</c>, <c>0-30/10</c>), month and weekday names (<c>JAN</c>, <c>MON</c>),
/// <c>7</c> as Sunday and the macros <c>@yearly</c>, <c>@monthly</c>, <c>@weekly</c>, <c>@daily</c>, <c>@hourly</c>.
/// When both day fields are restricted a day matches if either matches (Vixie cron semantics). Occurrences are
/// computed in a time zone; local times skipped by a daylight saving gap do not fire and repeated local times fire once.
/// </summary>
public sealed class CronExpression
{
    private static readonly string[] s_monthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
    private static readonly string[] s_dayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    private readonly ulong _minutes;
    private readonly ulong _hours;
    private readonly ulong _days;
    private readonly ulong _months;
    private readonly ulong _weekdays;
    private readonly bool _anyDay;
    private readonly bool _anyWeekday;

    private CronExpression(string expression, ulong minutes, ulong hours, ulong days, ulong months, ulong weekdays, bool anyDay, bool anyWeekday)
    {
        Expression = expression;
        _minutes = minutes;
        _hours = hours;
        _days = days;
        _months = months;
        _weekdays = weekdays;
        _anyDay = anyDay;
        _anyWeekday = anyWeekday;
    }

    /// <summary>Gets the normalized source expression.</summary>
    public string Expression { get; }

    /// <summary>Parses an expression.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The parsed expression.</returns>
    /// <exception cref="FormatException">The expression is invalid.</exception>
    public static CronExpression Parse(string expression) =>
        TryParse(expression, out var cron, out var error) ? cron! : throw new FormatException(error);

    /// <summary>Tries to parse an expression.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="cron">The parsed expression.</param>
    /// <param name="error">A user-facing error message when parsing fails.</param>
    /// <returns><see langword="true"/> when the expression is valid.</returns>
    public static bool TryParse(string? expression, out CronExpression? cron, out string? error)
    {
        cron = null;
        error = null;
        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "The cron expression is empty.";
            return false;
        }

        var normalized = string.Join(' ', expression.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var source = normalized.ToLowerInvariant() switch
        {
            "@yearly" or "@annually" => "0 0 1 1 *",
            "@monthly" => "0 0 1 * *",
            "@weekly" => "0 0 * * 0",
            "@daily" or "@midnight" => "0 0 * * *",
            "@hourly" => "0 * * * *",
            _ => normalized,
        };

        var parts = source.Split(' ');
        if (parts.Length != 5)
        {
            error = "A cron expression needs exactly 5 fields: minute hour day-of-month month day-of-week.";
            return false;
        }

        if (!TryParseField(parts[0], 0, 59, null, "minute", out var minutes, out error)
            || !TryParseField(parts[1], 0, 23, null, "hour", out var hours, out error)
            || !TryParseField(parts[2], 1, 31, null, "day-of-month", out var days, out error)
            || !TryParseField(parts[3], 1, 12, s_monthNames, "month", out var months, out error)
            || !TryParseField(parts[4], 0, 7, s_dayNames, "day-of-week", out var weekdays, out error))
        {
            return false;
        }

        // 7 is an alias for Sunday.
        if ((weekdays & (1UL << 7)) != 0)
        {
            weekdays = (weekdays | 1UL) & ~(1UL << 7);
        }

        cron = new CronExpression(normalized, minutes, hours, days, months, weekdays, parts[2].StartsWith('*'), parts[4].StartsWith('*'));
        return true;
    }

    /// <summary>Resolves a time zone identifier (IANA or Windows); empty or unknown identifiers yield UTC.</summary>
    /// <param name="timeZoneId">The identifier.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns><see langword="true"/> when the identifier was empty or found.</returns>
    public static bool TryFindTimeZone(string? timeZoneId, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZoneId) || string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>Checks whether a local wall-clock time (minute precision) matches the expression.</summary>
    /// <param name="local">The local time.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public bool Matches(DateTime local) =>
        Has(_minutes, local.Minute) && Has(_hours, local.Hour) && Has(_months, local.Month) && DayMatches(local);

    /// <summary>Gets the next occurrence strictly after a point in time.</summary>
    /// <param name="after">The reference time.</param>
    /// <param name="zone">The time zone of the expression (UTC when null).</param>
    /// <returns>The next occurrence in UTC, or <see langword="null"/> when none exists within five years.</returns>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Utc;
        var start = TimeZoneInfo.ConvertTime(after, zone).DateTime;
        var local = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0, DateTimeKind.Unspecified).AddMinutes(1);
        var limit = local.AddYears(5);

        while (local <= limit)
        {
            if (!Has(_months, local.Month))
            {
                local = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
                continue;
            }

            if (!DayMatches(local))
            {
                local = local.Date.AddDays(1);
                continue;
            }

            if (!Has(_hours, local.Hour))
            {
                var nextHour = NextBit(_hours, local.Hour + 1, 23);
                local = nextHour < 0 ? local.Date.AddDays(1) : local.Date.AddHours(nextHour);
                continue;
            }

            if (!Has(_minutes, local.Minute))
            {
                var nextMinute = NextBit(_minutes, local.Minute + 1, 59);
                local = nextMinute < 0
                    ? local.Date.AddHours(local.Hour + 1)
                    : local.Date.AddHours(local.Hour).AddMinutes(nextMinute);
                continue;
            }

            if (zone.IsInvalidTime(local))
            {
                local = local.AddMinutes(1);
                continue;
            }

            var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
            var candidate = new DateTimeOffset(local, offset).ToUniversalTime();
            if (candidate > after)
            {
                return candidate;
            }

            local = local.AddMinutes(1);
        }

        return null;
    }

    /// <summary>Gets the next occurrences after a point in time.</summary>
    /// <param name="after">The reference time.</param>
    /// <param name="count">The number of occurrences (1–100).</param>
    /// <param name="zone">The time zone (UTC when null).</param>
    /// <returns>The occurrences in UTC.</returns>
    public IReadOnlyList<DateTimeOffset> GetNextOccurrences(DateTimeOffset after, int count, TimeZoneInfo? zone = null)
    {
        var result = new List<DateTimeOffset>();
        var current = after;
        for (var i = 0; i < Math.Clamp(count, 1, 100); i++)
        {
            if (GetNextOccurrence(current, zone) is not { } next)
            {
                break;
            }

            result.Add(next);
            current = next;
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => Expression;

    private static bool Has(ulong mask, int value) => (mask & (1UL << value)) != 0;

    private static int NextBit(ulong mask, int from, int max)
    {
        for (var i = from; i <= max; i++)
        {
            if (Has(mask, i))
            {
                return i;
            }
        }

        return -1;
    }

    private bool DayMatches(DateTime local)
    {
        var dayOfMonth = Has(_days, local.Day);
        var dayOfWeek = Has(_weekdays, (int)local.DayOfWeek);
        if (_anyDay && _anyWeekday)
        {
            return true;
        }

        if (_anyDay)
        {
            return dayOfWeek;
        }

        return _anyWeekday ? dayOfMonth : dayOfMonth || dayOfWeek;
    }

    private static bool TryParseField(string field, int min, int max, string[]? names, string label, out ulong mask, out string? error)
    {
        mask = 0;
        error = null;
        foreach (var item in field.Split(','))
        {
            if (item.Length == 0)
            {
                error = $"The {label} field contains an empty list entry.";
                return false;
            }

            var step = 1;
            var rangePart = item;
            var slash = item.IndexOf('/', StringComparison.Ordinal);
            if (slash >= 0)
            {
                if (!int.TryParse(item[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out step) || step < 1 || step > max)
                {
                    error = $"The {label} field has an invalid step in '{item}'.";
                    return false;
                }

                rangePart = item[..slash];
            }

            int from;
            int to;
            if (rangePart == "*")
            {
                from = min;
                to = max == 7 ? 6 : max;
            }
            else
            {
                var dash = rangePart.IndexOf('-', StringComparison.Ordinal);
                if (dash > 0)
                {
                    if (!TryParseValue(rangePart[..dash], min, max, names, out from) || !TryParseValue(rangePart[(dash + 1)..], min, max, names, out to))
                    {
                        error = $"The {label} field has an invalid range '{rangePart}' (allowed {min}-{max}).";
                        return false;
                    }

                    if (from > to)
                    {
                        error = $"The {label} range '{rangePart}' must go from low to high.";
                        return false;
                    }
                }
                else
                {
                    if (!TryParseValue(rangePart, min, max, names, out from))
                    {
                        error = $"The {label} field has an invalid value '{rangePart}' (allowed {min}-{max}).";
                        return false;
                    }

                    to = slash >= 0 ? (max == 7 ? 6 : max) : from;
                }
            }

            for (var value = from; value <= to; value += step)
            {
                mask |= 1UL << value;
            }
        }

        return true;
    }

    private static bool TryParseValue(string text, int min, int max, string[]? names, out int value)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return value >= min && value <= max;
        }

        if (names is not null)
        {
            var index = Array.FindIndex(names, n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                value = index + (min == 1 ? 1 : 0);
                return true;
            }
        }

        return false;
    }
}
