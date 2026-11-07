using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>The computed result of a report.</summary>
public sealed record ReportResult
{
    /// <summary>Gets the table name.</summary>
    public required string TableName { get; init; }

    /// <summary>Gets the table label (plural).</summary>
    public required string TableLabel { get; init; }

    /// <summary>Gets the label of the grouping field (null when not grouped).</summary>
    public string? GroupByLabel { get; init; }

    /// <summary>Gets a label of the aggregate, for example <c>Count</c> or <c>Average of Criticality</c>.</summary>
    public required string AggregateLabel { get; init; }

    /// <summary>Gets the aggregate function.</summary>
    public ReportAggregate Aggregate { get; init; }

    /// <summary>Gets the requested chart type.</summary>
    public ReportChartType ChartType { get; init; }

    /// <summary>Gets the groups (chronological for date groupings, otherwise by value descending).</summary>
    public IReadOnlyList<ReportRow> Rows { get; init; } = [];

    /// <summary>Gets the aggregate over all records.</summary>
    public double Total { get; init; }

    /// <summary>Gets the number of records evaluated.</summary>
    public int RecordCount { get; init; }

    /// <summary>Gets a value indicating whether the row limit was reached (result based on a subset).</summary>
    public bool Truncated { get; init; }

    /// <summary>Gets a value indicating whether the grouping is a date series.</summary>
    public bool IsDateSeries { get; init; }

    /// <summary>Gets warnings to show with the result (for example unsupported filters).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Gets the UTC generation time.</summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Gets the execution time in milliseconds.</summary>
    public long DurationMs { get; init; }
}
