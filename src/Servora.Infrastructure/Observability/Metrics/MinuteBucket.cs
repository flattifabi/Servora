namespace Servora.Infrastructure.Observability.Metrics;

/// <summary>Mutable aggregation of the requests of one minute (guarded by the collector's lock).</summary>
internal sealed class MinuteBucket
{
    /// <summary>Upper bounds (ms) of the latency histogram buckets; the last bucket is unbounded.</summary>
    internal static readonly double[] Bounds = [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000];

    /// <summary>Gets or sets the minute index (Unix minutes) this bucket currently represents.</summary>
    public long MinuteIndex { get; set; } = -1;

    /// <summary>Gets or sets the request count.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets the number of 4xx responses.</summary>
    public int ClientErrors { get; set; }

    /// <summary>Gets or sets the number of 5xx responses.</summary>
    public int ServerErrors { get; set; }

    /// <summary>Gets or sets the summed duration.</summary>
    public double TotalMs { get; set; }

    /// <summary>Gets or sets the maximum duration.</summary>
    public double MaxMs { get; set; }

    /// <summary>Gets the latency histogram (one slot per bound plus overflow).</summary>
    public int[] Histogram { get; } = new int[Bounds.Length + 1];

    /// <summary>Resets the bucket for a new minute.</summary>
    /// <param name="minuteIndex">The new minute index.</param>
    public void Reset(long minuteIndex)
    {
        MinuteIndex = minuteIndex;
        Count = 0;
        ClientErrors = 0;
        ServerErrors = 0;
        TotalMs = 0;
        MaxMs = 0;
        Array.Clear(Histogram);
    }

    /// <summary>Adds a request.</summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="elapsedMs">The duration.</param>
    public void Add(int statusCode, double elapsedMs)
    {
        Count++;
        TotalMs += elapsedMs;
        MaxMs = Math.Max(MaxMs, elapsedMs);
        if (statusCode >= 500)
        {
            ServerErrors++;
        }
        else if (statusCode >= 400)
        {
            ClientErrors++;
        }

        var slot = Array.FindIndex(Bounds, b => elapsedMs <= b);
        Histogram[slot < 0 ? Bounds.Length : slot]++;
    }

    /// <summary>Estimates a percentile from a histogram (upper bound of the bucket containing the rank).</summary>
    /// <param name="histogram">The histogram.</param>
    /// <param name="count">The number of samples.</param>
    /// <param name="max">The maximum sample (used for the overflow bucket and as upper cap).</param>
    /// <param name="percentile">The percentile (0..1).</param>
    /// <returns>The estimate in milliseconds.</returns>
    public static double Percentile(int[] histogram, int count, double max, double percentile)
    {
        if (count == 0)
        {
            return 0;
        }

        var rank = Math.Ceiling(count * percentile);
        var cumulative = 0;
        for (var i = 0; i < histogram.Length; i++)
        {
            cumulative += histogram[i];
            if (cumulative >= rank)
            {
                return i < Bounds.Length ? Math.Min(Bounds[i], max) : max;
            }
        }

        return max;
    }
}
