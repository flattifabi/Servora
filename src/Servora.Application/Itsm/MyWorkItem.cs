using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>A work item (incident or problem) in the personal "My Work" list.</summary>
/// <param name="Table">The technical table name.</param>
/// <param name="Id">The record identifier.</param>
/// <param name="Number">The record number.</param>
/// <param name="ShortDescription">The summary.</param>
/// <param name="StateLabel">The state label.</param>
/// <param name="Priority">The priority.</param>
/// <param name="IsFlagged">Whether the record is flagged (major incident or known error).</param>
/// <param name="GroupName">The assignment group name.</param>
/// <param name="OpenedAt">The UTC time the record was opened.</param>
/// <param name="UpdatedAt">The UTC time of the last update.</param>
/// <param name="Route">The UI route of the record.</param>
public sealed record MyWorkItem(
    string Table,
    Guid Id,
    string Number,
    string ShortDescription,
    string StateLabel,
    Priority Priority,
    bool IsFlagged,
    string? GroupName,
    DateTimeOffset OpenedAt,
    DateTimeOffset UpdatedAt,
    string Route);
