using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>List item of a saved report.</summary>
/// <param name="Id">The report id.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="TableName">The source table.</param>
/// <param name="TableLabel">The source table label (or the name when the table is not installed).</param>
/// <param name="TableIcon">The table icon.</param>
/// <param name="ChartType">The chart type.</param>
/// <param name="GroupBy">The grouping field.</param>
/// <param name="Visibility">The visibility.</param>
/// <param name="SharedWithRole">The role the report is shared with.</param>
/// <param name="OwnerName">The owner display name.</param>
/// <param name="IsMine">Whether the current user owns the report.</param>
/// <param name="IsPinned">Whether the report is pinned to the current user's dashboard.</param>
/// <param name="IsSystem">Whether the report is a built-in sample.</param>
/// <param name="TableAvailable">Whether the source table exists (module installed).</param>
/// <param name="UpdatedAt">The last modification time.</param>
public sealed record ReportSummary(
    Guid Id,
    string Name,
    string? Description,
    string TableName,
    string TableLabel,
    string TableIcon,
    ReportChartType ChartType,
    string? GroupBy,
    ReportVisibility Visibility,
    string? SharedWithRole,
    string OwnerName,
    bool IsMine,
    bool IsPinned,
    bool IsSystem,
    bool TableAvailable,
    DateTimeOffset UpdatedAt);
