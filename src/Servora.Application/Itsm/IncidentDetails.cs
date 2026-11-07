using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>An incident with resolved display values for the record page.</summary>
public sealed class IncidentDetails
{
    /// <summary>Gets the record identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public required string Number { get; init; }

    /// <summary>Gets the optimistic concurrency version (pass it back on update).</summary>
    public required long Version { get; init; }

    /// <summary>Gets the state.</summary>
    public required IncidentState State { get; init; }

    /// <summary>Gets the priority.</summary>
    public required Priority Priority { get; init; }

    /// <summary>Gets the editable field values.</summary>
    public required IncidentInput Fields { get; init; }

    /// <summary>Gets the caller's contact card.</summary>
    public ItsmPerson? Caller { get; init; }

    /// <summary>Gets the assignee's display name.</summary>
    public string? AssignedToName { get; init; }

    /// <summary>Gets the assignment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the configuration item name.</summary>
    public string? ConfigurationItemName { get; init; }

    /// <summary>Gets the configuration item class.</summary>
    public string? ConfigurationItemClass { get; init; }

    /// <summary>Gets the business service name.</summary>
    public string? BusinessServiceName { get; init; }

    /// <summary>Gets the parent incident number.</summary>
    public string? ParentIncidentNumber { get; init; }

    /// <summary>Gets the linked problem identifier.</summary>
    public Guid? ProblemId { get; init; }

    /// <summary>Gets the linked problem number.</summary>
    public string? ProblemNumber { get; init; }

    /// <summary>Gets the linked problem summary.</summary>
    public string? ProblemShortDescription { get; init; }

    /// <summary>Gets the linked problem state.</summary>
    public ProblemState? ProblemState { get; init; }

    /// <summary>Gets the on-hold reason.</summary>
    public OnHoldReason? OnHoldReason { get; init; }

    /// <summary>Gets the resolution code.</summary>
    public IncidentResolutionCode? ResolutionCode { get; init; }

    /// <summary>Gets the resolution notes.</summary>
    public string? ResolutionNotes { get; init; }

    /// <summary>Gets the reopen count.</summary>
    public int ReopenCount { get; init; }

    /// <summary>Gets the reassignment count.</summary>
    public int ReassignmentCount { get; init; }

    /// <summary>Gets the number of child incidents.</summary>
    public int ChildCount { get; init; }

    /// <summary>Gets the UTC time the incident was opened.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the display name of the user that opened the incident.</summary>
    public string? OpenedByName { get; init; }

    /// <summary>Gets the UTC resolution time.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>Gets the display name of the resolver.</summary>
    public string? ResolvedByName { get; init; }

    /// <summary>Gets the UTC closure time.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets the UTC time of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the actor that last updated the incident.</summary>
    public string? UpdatedBy { get; init; }

    /// <summary>Gets the state label.</summary>
    public string StateLabel => ItsmLabels.For(State);

    /// <summary>Gets a value indicating whether the incident is still active.</summary>
    public bool IsActive => IncidentStateMachine.IsActive(State);
}
