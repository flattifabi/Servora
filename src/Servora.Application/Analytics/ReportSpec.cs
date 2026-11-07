using Servora.Application.Conditions;
using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>What a report computes: source table, filter, grouping, aggregate and visualization.</summary>
public sealed record ReportSpec
{
    /// <summary>The maximum number of groups a report may show.</summary>
    public const int MaxGroupLimit = 50;

    /// <summary>Gets the technical table name.</summary>
    public required string TableName { get; init; }

    /// <summary>Gets the filter condition (null or empty = all records).</summary>
    public ConditionGroup? Condition { get; init; }

    /// <summary>Gets the field to group by (null = single aggregate).</summary>
    public string? GroupBy { get; init; }

    /// <summary>Gets the bucket size for date groupings.</summary>
    public ReportDateInterval DateInterval { get; init; } = ReportDateInterval.Day;

    /// <summary>Gets the aggregate function.</summary>
    public ReportAggregate Aggregate { get; init; } = ReportAggregate.Count;

    /// <summary>Gets the numeric field for sum / average / min / max.</summary>
    public string? AggregateField { get; init; }

    /// <summary>Gets the chart type.</summary>
    public ReportChartType ChartType { get; init; } = ReportChartType.Bar;

    /// <summary>Gets the maximum number of groups (2..<see cref="MaxGroupLimit"/>); the rest is folded into "Other".</summary>
    public int MaxGroups { get; init; } = 12;

    /// <summary>Creates a specification from a saved report.</summary>
    /// <param name="report">The saved report.</param>
    /// <returns>The specification.</returns>
    public static ReportSpec From(ReportDefinition report) => new()
    {
        TableName = report.TableName,
        Condition = ConditionSerializer.Deserialize(report.ConditionJson),
        GroupBy = report.GroupBy,
        DateInterval = report.DateInterval,
        Aggregate = report.Aggregate,
        AggregateField = report.AggregateField,
        ChartType = report.ChartType,
        MaxGroups = report.MaxGroups,
    };
}
