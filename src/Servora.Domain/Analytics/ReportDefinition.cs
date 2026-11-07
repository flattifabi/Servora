using Servora.Domain.Common;

namespace Servora.Domain.Analytics;

/// <summary>
/// A saved report built with the report builder: source table, filter condition, grouping, aggregate and chart type.
/// Reports are configuration (not a record table) and are only accessible through the report service, which enforces
/// visibility and the table permissions of the viewer.
/// </summary>
public class ReportDefinition : AuditableEntity
{
    /// <summary>Gets or sets the report name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the technical name of the source table.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the filter condition as JSON (see <c>ConditionSerializer</c>); null means all records.</summary>
    public string? ConditionJson { get; set; }

    /// <summary>Gets or sets the field to group by; null produces a single aggregate value.</summary>
    public string? GroupBy { get; set; }

    /// <summary>Gets or sets the bucket size when <see cref="GroupBy"/> is a date field.</summary>
    public ReportDateInterval DateInterval { get; set; } = ReportDateInterval.Day;

    /// <summary>Gets or sets the aggregate function.</summary>
    public ReportAggregate Aggregate { get; set; } = ReportAggregate.Count;

    /// <summary>Gets or sets the numeric field aggregated by sum / average / min / max.</summary>
    public string? AggregateField { get; set; }

    /// <summary>Gets or sets the chart type.</summary>
    public ReportChartType ChartType { get; set; } = ReportChartType.Bar;

    /// <summary>Gets or sets the maximum number of groups shown (the rest is summarized as "Other").</summary>
    public int MaxGroups { get; set; } = 12;

    /// <summary>Gets or sets who can see the report.</summary>
    public ReportVisibility Visibility { get; set; } = ReportVisibility.Private;

    /// <summary>Gets or sets the role name the report is shared with when <see cref="Visibility"/> is <see cref="ReportVisibility.Role"/>.</summary>
    public string? SharedWithRole { get; set; }

    /// <summary>Gets or sets the owning user.</summary>
    [Reference(TableNames.User)]
    public Guid OwnerId { get; set; }

    /// <summary>Gets or sets the owner display name (denormalized for lists).</summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the report is a built-in sample shipped with Servora.</summary>
    public bool IsSystem { get; set; }
}
