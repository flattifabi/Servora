using System.Globalization;
using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>
/// Pure in-memory grouping and aggregation of projected report rows: groups by formatted key, computes the aggregate,
/// sorts (chronologically for date series, otherwise by value descending) and folds small groups into "Other".
/// </summary>
public static class ReportAggregator
{
    /// <summary>The key of the folded "Other" group.</summary>
    public const string OtherKey = "__other";

    /// <summary>The maximum number of buckets of a date series.</summary>
    public const int MaxDateBuckets = 400;

    /// <summary>Groups and aggregates rows.</summary>
    /// <param name="rows">The projected rows.</param>
    /// <param name="formatKey">Formats a raw key into a group key.</param>
    /// <param name="aggregate">The aggregate function.</param>
    /// <param name="maxGroups">The maximum number of groups for categorical groupings (including "Other").</param>
    /// <param name="chronological">Whether the grouping is a date series (sorted by time, never folded).</param>
    /// <param name="total">The aggregate over all rows.</param>
    /// <returns>The groups.</returns>
    public static IReadOnlyList<ReportRow> Aggregate(
        IEnumerable<ReportProjection> rows,
        Func<object?, ReportGroupKey> formatKey,
        ReportAggregate aggregate,
        int maxGroups,
        bool chronological,
        out double total)
    {
        var groups = new Dictionary<string, ReportGroupAccumulator>(StringComparer.Ordinal);
        var all = new ReportGroupAccumulator { Key = new ReportGroupKey(string.Empty, "All", null) };
        foreach (var row in rows)
        {
            var key = formatKey(row.Key);
            if (!groups.TryGetValue(key.Key, out var group))
            {
                group = new ReportGroupAccumulator { Key = key };
                groups[key.Key] = group;
            }

            var value = ToDouble(row.Value);
            group.Add(value);
            all.Add(value);
        }

        total = all.Result(aggregate);
        if (chronological)
        {
            return groups.Values
                .OrderBy(g => g.Key.SortKey is null)
                .ThenBy(g => g.Key.SortKey, Comparer<IComparable?>.Create(CompareSortKeys))
                .ThenBy(g => g.Key.Key, StringComparer.Ordinal)
                .TakeLast(MaxDateBuckets)
                .Select(g => ToRow(g, aggregate))
                .ToList();
        }

        var ordered = groups.Values
            .OrderByDescending(g => g.Result(aggregate))
            .ThenBy(g => g.Key.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        maxGroups = Math.Clamp(maxGroups, 2, ReportSpec.MaxGroupLimit);
        if (ordered.Count <= maxGroups)
        {
            return ordered.Select(g => ToRow(g, aggregate)).ToList();
        }

        var other = new ReportGroupAccumulator { Key = new ReportGroupKey(OtherKey, "Other", null) };
        foreach (var group in ordered.Skip(maxGroups - 1))
        {
            other.Merge(group);
        }

        return ordered.Take(maxGroups - 1).Append(other).Select(g => ToRow(g, aggregate)).ToList();
    }

    /// <summary>Converts a raw value to a number (numbers, booleans, enums); other values yield null.</summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The number or null.</returns>
    public static double? ToDouble(object? value) => value switch
    {
        null => null,
        double d => d,
        float f => f,
        decimal m => (double)m,
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        bool b => b ? 1 : 0,
        Enum e => Convert.ToDouble(e, CultureInfo.InvariantCulture),
        string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    private static int CompareSortKeys(IComparable? left, IComparable? right)
    {
        if (left is null || right is null)
        {
            return (left is null).CompareTo(right is null);
        }

        return left.GetType() == right.GetType() ? left.CompareTo(right) : string.CompareOrdinal(left.ToString(), right.ToString());
    }

    private static ReportRow ToRow(ReportGroupAccumulator group, ReportAggregate aggregate) =>
        new(group.Key.Key, group.Key.Label, Math.Round(group.Result(aggregate), 4), group.Count);
}
