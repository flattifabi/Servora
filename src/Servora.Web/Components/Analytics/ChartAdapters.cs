using System.Globalization;
using Servora.Application.Analytics;
using Servora.Domain.Analytics;
using Servora.Web.Components.UI.Charts;

namespace Servora.Web.Components.Analytics;

/// <summary>Converts analytics DTOs into the chart models of the design system.</summary>
public static class ChartAdapters
{
    /// <summary>Converts analytics series.</summary>
    /// <param name="series">The series.</param>
    /// <returns>The chart series.</returns>
    public static IReadOnlyList<ChartSeries> ToSeries(IEnumerable<AnalyticsSeries> series) =>
        series.Select(s => new ChartSeries(s.Name, s.Values, s.Color)).ToList();

    /// <summary>Converts analytics slices.</summary>
    /// <param name="slices">The slices.</param>
    /// <returns>The chart slices.</returns>
    public static IReadOnlyList<ChartSlice> ToSlices(IEnumerable<AnalyticsSlice> slices) =>
        slices.Select(s => new ChartSlice(s.Label, s.Value, s.Color)).ToList();

    /// <summary>Converts report rows into donut slices.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The slices.</returns>
    public static IReadOnlyList<ChartSlice> ToSlices(IEnumerable<ReportRow> rows) =>
        rows.Select(r => new ChartSlice(r.Label, r.Value, r.Key == ReportAggregator.OtherKey ? "#64748b" : null)).ToList();

    /// <summary>Gets the icon of a chart type.</summary>
    /// <param name="chartType">The chart type.</param>
    /// <returns>The icon name.</returns>
    public static string IconOf(ReportChartType chartType) => chartType switch
    {
        ReportChartType.Line => "trending-up",
        ReportChartType.Donut => "pie-chart",
        ReportChartType.Table => "table",
        ReportChartType.Metric => "target",
        ReportChartType.HorizontalBar => "list",
        _ => "bar-chart",
    };

    /// <summary>Gets the display name of a chart type.</summary>
    /// <param name="chartType">The chart type.</param>
    /// <returns>The label.</returns>
    public static string LabelOf(ReportChartType chartType) => chartType switch
    {
        ReportChartType.HorizontalBar => "Ranking",
        ReportChartType.Line => "Trend",
        ReportChartType.Donut => "Donut",
        ReportChartType.Table => "Table",
        ReportChartType.Metric => "Metric",
        _ => "Bar",
    };

    /// <summary>Formats an aggregated value (integers without decimals, others with up to two).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string FormatValue(double value) =>
        Math.Abs(value % 1) < 0.0001 ? value.ToString("N0", CultureInfo.CurrentCulture) : value.ToString("N2", CultureInfo.CurrentCulture);

    /// <summary>Formats a byte count.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text, for example <c>12.4 MB</c>.</returns>
    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : "0.0", CultureInfo.CurrentCulture) + " " + units[unit];
    }

    /// <summary>Formats a duration compactly (for example <c>3 d 4 h</c>).</summary>
    /// <param name="span">The duration.</param>
    /// <returns>The text.</returns>
    public static string FormatDuration(TimeSpan span) =>
        span.TotalDays >= 1 ? $"{(int)span.TotalDays} d {span.Hours} h"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} min"
        : $"{(int)span.TotalMinutes} min {span.Seconds} s";
}
