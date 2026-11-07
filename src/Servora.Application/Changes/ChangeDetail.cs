using Servora.Domain.Approvals;
using Servora.Domain.Changes;
using Servora.Domain.Itsm;

namespace Servora.Application.Changes;

/// <summary>Full view of a change request for the record page.</summary>
public sealed record ChangeDetail
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the short description.</summary>
    public string ShortDescription { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the type.</summary>
    public ChangeType Type { get; init; }

    /// <summary>Gets the state.</summary>
    public ChangeState State { get; init; }

    /// <summary>Gets the category.</summary>
    public string? Category { get; init; }

    /// <summary>Gets the impact.</summary>
    public Impact Impact { get; init; }

    /// <summary>Gets the urgency.</summary>
    public Urgency Urgency { get; init; }

    /// <summary>Gets the priority.</summary>
    public Priority Priority { get; init; }

    /// <summary>Gets the risk.</summary>
    public ChangeRisk Risk { get; init; }

    /// <summary>Gets the risk score.</summary>
    public int RiskScore { get; init; }

    /// <summary>Gets the time of the last risk assessment.</summary>
    public DateTimeOffset? RiskAssessedAt { get; init; }

    /// <summary>Gets the risk questionnaire answers.</summary>
    public IReadOnlyDictionary<string, int> RiskAnswers { get; init; } = new Dictionary<string, int>();

    /// <summary>Gets the assignment group.</summary>
    public Guid? AssignmentGroupId { get; init; }

    /// <summary>Gets the assignment group name.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the assignee.</summary>
    public Guid? AssignedToId { get; init; }

    /// <summary>Gets the assignee name.</summary>
    public string? AssignedToName { get; init; }

    /// <summary>Gets the requester.</summary>
    public Guid? RequestedById { get; init; }

    /// <summary>Gets the requester name.</summary>
    public string? RequestedByName { get; init; }

    /// <summary>Gets the configuration item.</summary>
    public Guid? ConfigurationItemId { get; init; }

    /// <summary>Gets the configuration item name.</summary>
    public string? ConfigurationItemName { get; init; }

    /// <summary>Gets the approval group.</summary>
    public Guid? ApprovalGroupId { get; init; }

    /// <summary>Gets the effective approval group name (the configured group or the change advisory board).</summary>
    public string? ApprovalGroupName { get; init; }

    /// <summary>Gets the aggregated approval status.</summary>
    public ApprovalStatus Approval { get; init; }

    /// <summary>Gets the planned start.</summary>
    public DateTimeOffset? PlannedStartAt { get; init; }

    /// <summary>Gets the planned end.</summary>
    public DateTimeOffset? PlannedEndAt { get; init; }

    /// <summary>Gets the actual start.</summary>
    public DateTimeOffset? ActualStartAt { get; init; }

    /// <summary>Gets the actual end.</summary>
    public DateTimeOffset? ActualEndAt { get; init; }

    /// <summary>Gets the justification.</summary>
    public string? Justification { get; init; }

    /// <summary>Gets the implementation plan.</summary>
    public string? ImplementationPlan { get; init; }

    /// <summary>Gets the backout plan.</summary>
    public string? BackoutPlan { get; init; }

    /// <summary>Gets the test plan.</summary>
    public string? TestPlan { get; init; }

    /// <summary>Gets the close code.</summary>
    public ChangeCloseCode? CloseCode { get; init; }

    /// <summary>Gets the close notes.</summary>
    public string? CloseNotes { get; init; }

    /// <summary>Gets the opening time.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the name of the user that opened the change.</summary>
    public string? OpenedByName { get; init; }

    /// <summary>Gets the closing time.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the optimistic concurrency version.</summary>
    public long Version { get; init; }

    /// <summary>Gets the change tasks.</summary>
    public IReadOnlyList<ChangeTaskItem> Tasks { get; init; } = [];

    /// <summary>Gets the conflicting changes.</summary>
    public IReadOnlyList<ChangeConflict> Conflicts { get; init; } = [];

    /// <summary>Gets the states the change can move to.</summary>
    public IReadOnlyList<ChangeState> NextStates { get; init; } = [];

    /// <summary>Gets a value indicating whether the change can still be edited.</summary>
    public bool IsEditable => !ChangeStateMachine.IsFinal(State);
}
