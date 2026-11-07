namespace Servora.Domain.Analytics;

/// <summary>Visualization of a report result.</summary>
public enum ReportChartType
{
    /// <summary>Vertical bar chart.</summary>
    Bar = 0,

    /// <summary>Horizontal bar list (best for long labels).</summary>
    HorizontalBar = 1,

    /// <summary>Line / area chart (best for date groupings).</summary>
    Line = 2,

    /// <summary>Donut chart (share of total).</summary>
    Donut = 3,

    /// <summary>Tabular result.</summary>
    Table = 4,

    /// <summary>Single big number (no grouping).</summary>
    Metric = 5,
}
