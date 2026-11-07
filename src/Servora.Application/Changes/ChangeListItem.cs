using Servora.Domain.Approvals;
using Servora.Domain.Changes;
using Servora.Domain.Itsm;

namespace Servora.Application.Changes;

/// <summary>A row of the change list.</summary>
public sealed record ChangeListItem
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the short description.</summary>
    public string ShortDescription { get; init; } = string.Empty;

    /// <summary>Gets the type.</summary>
    public ChangeType Type { get; init; }

    /// <summary>Gets the state.</summary>
    public ChangeState State { get; init; }

    /// <summary>Gets the risk.</summary>
    public ChangeRisk Risk { get; init; }

    /// <summary>Gets the priority.</summary>
    public Priority Priority { get; init; }

    /// <summary>Gets the approval status.</summary>
    public ApprovalStatus Approval { get; init; }

    /// <summary>Gets the assignment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the assignee name.</summary>
    public string? AssignedToName { get; init; }

    /// <summary>Gets the configuration item name.</summary>
    public string? ConfigurationItemName { get; init; }

    /// <summary>Gets the planned start.</summary>
    public DateTimeOffset? PlannedStartAt { get; init; }

    /// <summary>Gets the planned end.</summary>
    public DateTimeOffset? PlannedEndAt { get; init; }

    /// <summary>Gets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
