namespace Servora.Application.Analytics;

/// <summary>Data of one dashboard chart. Charts that cannot be computed carry an availability state and a message.</summary>
public sealed record AnalyticsChart
{
    /// <summary>Gets the availability.</summary>
    public AnalyticsAvailability Availability { get; init; } = AnalyticsAvailability.Ready;

    /// <summary>Gets the explanation for unavailable charts.</summary>
    public string? Message { get; init; }

    /// <summary>Gets the x-axis / category labels.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Gets the series (line / bar charts).</summary>
    public IReadOnlyList<AnalyticsSeries> Series { get; init; } = [];

    /// <summary>Gets the slices (donut charts, rankings).</summary>
    public IReadOnlyList<AnalyticsSlice> Slices { get; init; } = [];

    /// <summary>Gets a headline value (for example a percentage), if any.</summary>
    public double? Headline { get; init; }

    /// <summary>Gets a short caption for the headline.</summary>
    public string? Caption { get; init; }

    /// <summary>Creates an unavailable chart.</summary>
    /// <param name="availability">The availability state.</param>
    /// <param name="message">The explanation.</param>
    /// <returns>The chart.</returns>
    public static AnalyticsChart Unavailable(AnalyticsAvailability availability, string message) => new() { Availability = availability, Message = message };

    /// <summary>Creates a chart from series, or a "no data" chart when every value is zero.</summary>
    /// <param name="labels">The labels.</param>
    /// <param name="series">The series.</param>
    /// <param name="emptyMessage">The message when there is no data.</param>
    /// <returns>The chart.</returns>
    public static AnalyticsChart FromSeries(IReadOnlyList<string> labels, IReadOnlyList<AnalyticsSeries> series, string emptyMessage) =>
        series.All(s => s.Values.All(v => v == 0))
            ? new AnalyticsChart { Availability = AnalyticsAvailability.NoData, Message = emptyMessage, Labels = labels, Series = series }
            : new AnalyticsChart { Labels = labels, Series = series };

    /// <summary>Creates a chart from slices, or a "no data" chart when there are none.</summary>
    /// <param name="slices">The slices.</param>
    /// <param name="emptyMessage">The message when there is no data.</param>
    /// <returns>The chart.</returns>
    public static AnalyticsChart FromSlices(IReadOnlyList<AnalyticsSlice> slices, string emptyMessage) =>
        slices.Count == 0 || slices.All(s => s.Value == 0)
            ? new AnalyticsChart { Availability = AnalyticsAvailability.NoData, Message = emptyMessage }
            : new AnalyticsChart { Slices = slices };
}
