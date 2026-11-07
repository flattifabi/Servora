using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>Running statistics of one report group (count, sum, min, max of the non-null values).</summary>
internal sealed class ReportGroupAccumulator
{
    /// <summary>Gets the group key.</summary>
    public required ReportGroupKey Key { get; init; }

    /// <summary>Gets the number of records.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the number of non-null values.</summary>
    public int ValueCount { get; private set; }

    /// <summary>Gets the sum of the values.</summary>
    public double Sum { get; private set; }

    /// <summary>Gets the minimum value.</summary>
    public double Min { get; private set; } = double.MaxValue;

    /// <summary>Gets the maximum value.</summary>
    public double Max { get; private set; } = double.MinValue;

    /// <summary>Adds a record.</summary>
    /// <param name="value">The numeric value or null.</param>
    public void Add(double? value)
    {
        Count++;
        if (value is { } v && !double.IsNaN(v))
        {
            ValueCount++;
            Sum += v;
            Min = Math.Min(Min, v);
            Max = Math.Max(Max, v);
        }
    }

    /// <summary>Merges another group into this one (used for "Other").</summary>
    /// <param name="other">The other group.</param>
    public void Merge(ReportGroupAccumulator other)
    {
        Count += other.Count;
        ValueCount += other.ValueCount;
        Sum += other.Sum;
        if (other.ValueCount > 0)
        {
            Min = Math.Min(Min, other.Min);
            Max = Math.Max(Max, other.Max);
        }
    }

    /// <summary>Computes the aggregate.</summary>
    /// <param name="aggregate">The aggregate function.</param>
    /// <returns>The value (0 when there are no values).</returns>
    public double Result(ReportAggregate aggregate) => aggregate switch
    {
        ReportAggregate.Count => Count,
        ReportAggregate.Sum => Sum,
        ReportAggregate.Average => ValueCount == 0 ? 0 : Sum / ValueCount,
        ReportAggregate.Min => ValueCount == 0 ? 0 : Min,
        ReportAggregate.Max => ValueCount == 0 ? 0 : Max,
        _ => Count,
    };
}
