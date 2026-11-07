using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>Row of the problem list.</summary>
/// <param name="Id">The record identifier.</param>
/// <param name="Number">The number, for example <c>PRB0001001</c>.</param>
/// <param name="ShortDescription">The summary.</param>
/// <param name="State">The state.</param>
/// <param name="Priority">The priority.</param>
/// <param name="IsKnownError">Whether the problem is a known error.</param>
/// <param name="Category">The category name.</param>
/// <param name="AssignedToId">The assignee identifier.</param>
/// <param name="AssignedToName">The assignee's display name.</param>
/// <param name="AssignmentGroupName">The assignment group name.</param>
/// <param name="IncidentCount">The number of linked incidents.</param>
/// <param name="OpenedAt">The UTC time the problem was opened.</param>
/// <param name="UpdatedAt">The UTC time of the last update.</param>
public sealed record ProblemListItem(
    Guid Id,
    string Number,
    string ShortDescription,
    ProblemState State,
    Priority Priority,
    bool IsKnownError,
    string? Category,
    Guid? AssignedToId,
    string? AssignedToName,
    string? AssignmentGroupName,
    int IncidentCount,
    DateTimeOffset OpenedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Gets the state label.</summary>
    public string StateLabel => ItsmLabels.For(State);
}
