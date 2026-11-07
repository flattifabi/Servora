using Servora.Domain.Approvals;
using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>Facts about a change request that the <see cref="ChangeStateMachine"/> needs to validate a transition.</summary>
public sealed record ChangeTransitionContext
{
    /// <summary>Gets the change type.</summary>
    public ChangeType Type { get; init; }

    /// <summary>Gets the current state.</summary>
    public ChangeState Current { get; init; }

    /// <summary>Gets the requested target state.</summary>
    public ChangeState Target { get; init; }

    /// <summary>Gets the planned start.</summary>
    public DateTimeOffset? PlannedStartAt { get; init; }

    /// <summary>Gets the planned end.</summary>
    public DateTimeOffset? PlannedEndAt { get; init; }

    /// <summary>Gets a value indicating whether an implementation plan exists.</summary>
    public bool HasImplementationPlan { get; init; }

    /// <summary>Gets a value indicating whether a backout plan exists.</summary>
    public bool HasBackoutPlan { get; init; }

    /// <summary>Gets a value indicating whether the risk was assessed.</summary>
    public bool RiskAssessed { get; init; }

    /// <summary>Gets the aggregated approval status.</summary>
    public ApprovalStatus Approval { get; init; }

    /// <summary>Gets the number of change tasks that are not completed or cancelled.</summary>
    public int OpenTaskCount { get; init; }

    /// <summary>Gets the close code supplied with the transition.</summary>
    public ChangeCloseCode? CloseCode { get; init; }

    /// <summary>Gets the close notes supplied with the transition.</summary>
    public string? CloseNotes { get; init; }
}
