using Servora.Domain.Itsm;

namespace Servora.Application.Itsm;

/// <summary>A problem with resolved display values for the record page.</summary>
public sealed class ProblemDetails
{
    /// <summary>Gets the record identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public required string Number { get; init; }

    /// <summary>Gets the optimistic concurrency version (pass it back on update).</summary>
    public required long Version { get; init; }

    /// <summary>Gets the state.</summary>
    public required ProblemState State { get; init; }

    /// <summary>Gets the priority.</summary>
    public required Priority Priority { get; init; }

    /// <summary>Gets the editable field values.</summary>
    public required ProblemInput Fields { get; init; }

    /// <summary>Gets the assignee's contact card.</summary>
    public ItsmPerson? AssignedTo { get; init; }

    /// <summary>Gets the assignment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the configuration item name.</summary>
    public string? ConfigurationItemName { get; init; }

    /// <summary>Gets the business service name.</summary>
    public string? BusinessServiceName { get; init; }

    /// <summary>Gets the resolution code.</summary>
    public ProblemResolutionCode? ResolutionCode { get; init; }

    /// <summary>Gets the number of linked incidents.</summary>
    public int IncidentCount { get; init; }

    /// <summary>Gets the number of linked incidents that are still open.</summary>
    public int OpenIncidentCount { get; init; }

    /// <summary>Gets the UTC time the problem was opened.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the display name of the user that opened the problem.</summary>
    public string? OpenedByName { get; init; }

    /// <summary>Gets the UTC resolution time.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>Gets the display name of the resolver.</summary>
    public string? ResolvedByName { get; init; }

    /// <summary>Gets the UTC closure time.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets the UTC time of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the actor that last updated the problem.</summary>
    public string? UpdatedBy { get; init; }

    /// <summary>Gets the state label.</summary>
    public string StateLabel => ItsmLabels.For(State);

    /// <summary>Gets a value indicating whether the problem is still active.</summary>
    public bool IsActive => ProblemStateMachine.IsActive(State);
}
