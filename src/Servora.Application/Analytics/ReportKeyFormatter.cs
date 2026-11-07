using System.Globalization;
using Servora.Application.Tables;
using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>Turns raw group values into stable keys and readable labels (choice labels, reference display values, date buckets).</summary>
public static class ReportKeyFormatter
{
    /// <summary>The label of records without a value.</summary>
    public const string EmptyLabel = "(empty)";

    /// <summary>Creates a key formatter for a grouping field.</summary>
    /// <param name="field">The grouping field (null = single group).</param>
    /// <param name="interval">The date bucket size.</param>
    /// <param name="references">Display values of referenced records.</param>
    /// <returns>The formatter.</returns>
    public static Func<object?, ReportGroupKey> Create(FieldDefinition? field, ReportDateInterval interval, IReadOnlyDictionary<Guid, string>? references = null)
    {
        if (field is null)
        {
            return _ => new ReportGroupKey("all", "All", null);
        }

        var choices = field.Choices.ToDictionary(c => c.Value, c => c.Label, StringComparer.OrdinalIgnoreCase);
        return raw => Format(raw, field.Type, interval, choices, references);
    }

    /// <summary>Checks whether a field type is grouped as a date series.</summary>
    /// <param name="type">The field type.</param>
    /// <returns><see langword="true"/> for date / date-time fields.</returns>
    public static bool IsDateType(FieldType type) => type is FieldType.DateTime or FieldType.Date;

    /// <summary>Computes the bucket of a timestamp.</summary>
    /// <param name="value">The timestamp (UTC).</param>
    /// <param name="interval">The bucket size.</param>
    /// <returns>The bucket start and its label.</returns>
    public static (DateTime Start, string Label) DateBucket(DateTime value, ReportDateInterval interval)
    {
        var day = value.Date;
        return interval switch
        {
            ReportDateInterval.Week => WeekBucket(day),
            ReportDateInterval.Month => (new DateTime(day.Year, day.Month, 1), new DateTime(day.Year, day.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture)),
            ReportDateInterval.Quarter => QuarterBucket(day),
            ReportDateInterval.Year => (new DateTime(day.Year, 1, 1), day.Year.ToString(CultureInfo.InvariantCulture)),
            _ => (day, day.ToString("dd MMM", CultureInfo.InvariantCulture)),
        };
    }

    private static ReportGroupKey Format(object? raw, FieldType type, ReportDateInterval interval, Dictionary<string, string> choices, IReadOnlyDictionary<Guid, string>? references)
    {
        switch (raw)
        {
            case null:
                return new ReportGroupKey(string.Empty, EmptyLabel, null);
            case string text when string.IsNullOrWhiteSpace(text):
                return new ReportGroupKey(string.Empty, EmptyLabel, null);
            case bool flag:
                return new ReportGroupKey(flag ? "true" : "false", flag ? "Yes" : "No", flag ? 1 : 0);
            case Enum enumValue:
            {
                var numeric = Convert.ToInt64(enumValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                var label = choices.TryGetValue(numeric, out var choiceLabel) ? choiceLabel : enumValue.ToString();
                return new ReportGroupKey(numeric, label, Convert.ToInt64(enumValue, CultureInfo.InvariantCulture));
            }

            case Guid id:
            {
                var key = id.ToString();
                var label = references is not null && references.TryGetValue(id, out var display) ? display : "#" + id.ToString("N")[..8];
                return new ReportGroupKey(key, label, null);
            }

            case DateTimeOffset dto:
                return DateKey(dto.UtcDateTime, interval);
            case DateTime dt:
                return DateKey(dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt, interval);
            case DateOnly date:
                return DateKey(date.ToDateTime(TimeOnly.MinValue), interval);
        }

        var invariant = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        if (type == FieldType.Choice && choices.TryGetValue(invariant, out var mapped))
        {
            return new ReportGroupKey(invariant, mapped, null);
        }

        var number = ReportAggregator.ToDouble(raw);
        var trimmed = invariant.Trim();
        return new ReportGroupKey(trimmed, trimmed.Length > 80 ? trimmed[..80] + "…" : trimmed, number);
    }

    private static ReportGroupKey DateKey(DateTime value, ReportDateInterval interval)
    {
        var (start, label) = DateBucket(value, interval);
        return new ReportGroupKey(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), label, start);
    }

    private static (DateTime Start, string Label) WeekBucket(DateTime day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7;
        var start = day.AddDays(-offset);
        return (start, "Wk " + start.ToString("dd MMM", CultureInfo.InvariantCulture));
    }

    private static (DateTime Start, string Label) QuarterBucket(DateTime day)
    {
        var quarter = ((day.Month - 1) / 3) + 1;
        return (new DateTime(day.Year, ((quarter - 1) * 3) + 1, 1), string.Create(CultureInfo.InvariantCulture, $"Q{quarter} {day.Year}"));
    }
}
