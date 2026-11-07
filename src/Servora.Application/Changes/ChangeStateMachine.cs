using Servora.Domain.Approvals;
using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>
/// The change request lifecycle (pure logic, no I/O):
/// <c>New → Assess → Authorize → Scheduled → Implement → Review → Closed</c> for normal and emergency changes and
/// <c>New → Scheduled → Implement → Review → Closed</c> for pre-approved standard changes. Every non-final state can be
/// cancelled except <c>Implement</c> and <c>Review</c> (a started implementation must be reviewed and closed with a
/// close code). <c>Authorize → Scheduled</c> requires an approved CAB approval and normally happens automatically.
/// </summary>
public static class ChangeStateMachine
{
    private static readonly ChangeState[] s_normalFlow =
        [ChangeState.New, ChangeState.Assess, ChangeState.Authorize, ChangeState.Scheduled, ChangeState.Implement, ChangeState.Review, ChangeState.Closed];

    private static readonly ChangeState[] s_standardFlow =
        [ChangeState.New, ChangeState.Scheduled, ChangeState.Implement, ChangeState.Review, ChangeState.Closed];

    /// <summary>Gets the ordered main states of a change type (for the state progression bar).</summary>
    /// <param name="type">The change type.</param>
    /// <returns>The states.</returns>
    public static IReadOnlyList<ChangeState> GetProgression(ChangeType type) => type == ChangeType.Standard ? s_standardFlow : s_normalFlow;

    /// <summary>Checks whether a state is final.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for closed and cancelled.</returns>
    public static bool IsFinal(ChangeState state) => state is ChangeState.Closed or ChangeState.Cancelled;

    /// <summary>Gets the states a change can move to from its current state.</summary>
    /// <param name="type">The change type.</param>
    /// <param name="current">The current state.</param>
    /// <returns>The allowed target states.</returns>
    public static IReadOnlyList<ChangeState> GetNextStates(ChangeType type, ChangeState current) => current switch
    {
        ChangeState.New when type == ChangeType.Standard => [ChangeState.Scheduled, ChangeState.Cancelled],
        ChangeState.New => [ChangeState.Assess, ChangeState.Cancelled],
        ChangeState.Assess when type == ChangeType.Standard => [ChangeState.Scheduled, ChangeState.Cancelled],
        ChangeState.Assess => [ChangeState.Authorize, ChangeState.New, ChangeState.Cancelled],
        ChangeState.Authorize => [ChangeState.Scheduled, ChangeState.Assess, ChangeState.Cancelled],
        ChangeState.Scheduled => [ChangeState.Implement, ChangeState.Cancelled],
        ChangeState.Implement => [ChangeState.Review],
        ChangeState.Review => [ChangeState.Closed],
        _ => [],
    };

    /// <summary>Validates a transition.</summary>
    /// <param name="context">The transition facts.</param>
    /// <returns><see langword="null"/> when allowed; otherwise a user-safe reason.</returns>
    public static string? Validate(ChangeTransitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!GetNextStates(context.Type, context.Current).Contains(context.Target))
        {
            return $"A {context.Type.ToString().ToLowerInvariant()} change cannot move from {context.Current} to {context.Target}.";
        }

        var hasWindow = context.PlannedStartAt is { } start && context.PlannedEndAt is { } end && end > start;
        switch (context.Target)
        {
            case ChangeState.Authorize:
                if (!hasWindow)
                {
                    return "Plan the implementation window (start before end) before requesting authorization.";
                }

                if (!context.HasImplementationPlan || !context.HasBackoutPlan)
                {
                    return "An implementation plan and a backout plan are required before requesting authorization.";
                }

                if (context.Type == ChangeType.Normal && !context.RiskAssessed)
                {
                    return "Complete the risk assessment before requesting authorization.";
                }

                break;

            case ChangeState.Scheduled when context.Type == ChangeType.Standard:
                if (!hasWindow)
                {
                    return "Plan the implementation window (start before end) before scheduling the change.";
                }

                if (!context.HasImplementationPlan)
                {
                    return "An implementation plan is required before scheduling the change.";
                }

                break;

            case ChangeState.Scheduled:
                if (context.Approval != ApprovalStatus.Approved)
                {
                    return "The change can only be scheduled after it was approved by the change advisory board.";
                }

                break;

            case ChangeState.Closed:
                if (context.CloseCode is null || string.IsNullOrWhiteSpace(context.CloseNotes))
                {
                    return "A close code and close notes are required to close the change.";
                }

                if (context.OpenTaskCount > 0)
                {
                    return $"Complete or cancel the {context.OpenTaskCount} open change task(s) before closing the change.";
                }

                break;
        }

        return null;
    }
}
