using Servora.Domain.Integration;

namespace Servora.Application.Integration.Imports;

/// <summary>A row of the import set list.</summary>
/// <param name="Id">The import set.</param>
/// <param name="Name">The name.</param>
/// <param name="SourceType">The source type.</param>
/// <param name="TargetTable">The target table.</param>
/// <param name="CoalesceField">The coalesce field.</param>
/// <param name="ScheduleEnabled">Whether the schedule is active.</param>
/// <param name="NextRunAt">The next scheduled run.</param>
/// <param name="LastRunAt">The last run.</param>
public sealed record ImportSetListItem(
    Guid Id,
    string Name,
    ImportSourceType SourceType,
    string TargetTable,
    string? CoalesceField,
    bool ScheduleEnabled,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? LastRunAt)
{
    /// <summary>Gets the status of the latest run.</summary>
    public ImportRunStatus? LastRunStatus { get; init; }

    /// <summary>Gets the number of rows of the latest run.</summary>
    public int LastRunRows { get; init; }
}
