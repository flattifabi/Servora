using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>Row of the incident list and of related-record lists.</summary>
/// <param name="Id">The record identifier.</param>
/// <param name="Number">The number, for example <c>INC0001001</c>.</param>
/// <param name="ShortDescription">The summary.</param>
/// <param name="State">The state.</param>
/// <param name="Priority">The priority.</param>
/// <param name="IsMajorIncident">Whether the incident is a major incident.</param>
/// <param name="Category">The category name.</param>
/// <param name="CallerName">The caller's display name.</param>
/// <param name="AssignedToId">The assignee identifier.</param>
/// <param name="AssignedToName">The assignee's display name.</param>
/// <param name="AssignmentGroupName">The assignment group name.</param>
/// <param name="OpenedAt">The UTC time the incident was opened.</param>
/// <param name="UpdatedAt">The UTC time of the last update.</param>
public sealed record IncidentListItem(
    Guid Id,
    string Number,
    string ShortDescription,
    IncidentState State,
    Priority Priority,
    bool IsMajorIncident,
    string? Category,
    string? CallerName,
    Guid? AssignedToId,
    string? AssignedToName,
    string? AssignmentGroupName,
    DateTimeOffset OpenedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Gets the state label.</summary>
    public string StateLabel => ItsmLabels.For(State);
}
