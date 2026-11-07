using System.Globalization;

namespace Servora.Application.Analytics;

/// <summary>Daily (periods up to 92 days) or weekly time buckets of a dashboard period, with labels.</summary>
public sealed class TimeBuckets
{
    private readonly DateTime _start;
    private readonly int _size;

    /// <summary>Initializes a new instance of the <see cref="TimeBuckets"/> class.</summary>
    /// <param name="now">The current time.</param>
    /// <param name="periodDays">The period length in days.</param>
    public TimeBuckets(DateTimeOffset now, int periodDays)
    {
        _size = periodDays <= 92 ? 1 : 7;
        var today = now.UtcDateTime.Date;
        Count = (int)Math.Ceiling(periodDays / (double)_size);
        _start = today.AddDays(-((Count * _size) - 1));
        Start = new DateTimeOffset(_start, TimeSpan.Zero);
        PreviousStart = Start.AddDays(-(Count * _size));
        Labels = Enumerable.Range(0, Count)
            .Select(i => _start.AddDays(i * _size).ToString(_size == 1 ? "dd MMM" : "'Wk' dd MMM", CultureInfo.InvariantCulture))
            .ToList();
    }

    /// <summary>Gets the number of buckets.</summary>
    public int Count { get; }

    /// <summary>Gets the start of the current period (UTC midnight).</summary>
    public DateTimeOffset Start { get; }

    /// <summary>Gets the start of the previous period of the same length.</summary>
    public DateTimeOffset PreviousStart { get; }

    /// <summary>Gets the bucket labels.</summary>
    public IReadOnlyList<string> Labels { get; }

    /// <summary>Gets a value indicating whether buckets are weeks.</summary>
    public bool Weekly => _size == 7;

    /// <summary>Gets the bucket index of a timestamp, or -1 when it is outside the current period.</summary>
    /// <param name="value">The timestamp.</param>
    /// <returns>The index.</returns>
    public int IndexOf(DateTimeOffset value)
    {
        var days = (value.UtcDateTime.Date - _start).Days;
        if (days < 0)
        {
            return -1;
        }

        var index = days / _size;
        return index < Count ? index : -1;
    }

    /// <summary>Counts timestamps per bucket.</summary>
    /// <param name="values">The timestamps.</param>
    /// <returns>One count per bucket.</returns>
    public double[] CountPerBucket(IEnumerable<DateTimeOffset> values)
    {
        var result = new double[Count];
        foreach (var value in values)
        {
            var index = IndexOf(value);
            if (index >= 0)
            {
                result[index]++;
            }
        }

        return result;
    }
}
