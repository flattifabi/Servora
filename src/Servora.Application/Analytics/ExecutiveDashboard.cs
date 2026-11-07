namespace Servora.Application.Analytics;

/// <summary>The executive analytics dashboard computed by <see cref="IAnalyticsService"/>.</summary>
/// <param name="PeriodDays">The period length in days.</param>
/// <param name="GeneratedAt">The UTC generation time.</param>
/// <param name="DurationMs">The computation time in milliseconds.</param>
/// <param name="Kpis">The KPI tiles.</param>
/// <param name="Charts">The charts keyed by <see cref="AnalyticsChartKeys"/> constants.</param>
/// <param name="InstalledModules">Labels of the data sources (modules) found at runtime.</param>
public sealed record ExecutiveDashboard(
    int PeriodDays,
    DateTimeOffset GeneratedAt,
    long DurationMs,
    IReadOnlyList<AnalyticsKpi> Kpis,
    IReadOnlyDictionary<string, AnalyticsChart> Charts,
    IReadOnlyList<string> InstalledModules)
{
    /// <summary>Gets a chart by key (an unavailable placeholder when missing).</summary>
    /// <param name="key">The chart key.</param>
    /// <returns>The chart.</returns>
    public AnalyticsChart Chart(string key) =>
        Charts.TryGetValue(key, out var chart) ? chart : AnalyticsChart.Unavailable(AnalyticsAvailability.NoData, "No data.");
}
