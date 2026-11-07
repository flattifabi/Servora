using Servora.Domain.Approvals;

namespace Servora.Application.Approvals;

/// <summary>Pure policy logic that combines the decisions of one approval round into an outcome.</summary>
public static class ApprovalPolicyEvaluator
{
    /// <summary>Evaluates the outcome of an approval round.</summary>
    /// <param name="mode">How the decisions are combined.</param>
    /// <param name="states">The states of all approval rows of the round.</param>
    /// <returns>The aggregated status.</returns>
    /// <remarks>
    /// <para><see cref="ApprovalMode.AnyApprover"/>: one approval approves; otherwise one rejection rejects; otherwise the
    /// round is pending while decisions are open, else cancelled.</para>
    /// <para><see cref="ApprovalMode.AllApprovers"/>: one rejection rejects; a withdrawn row cancels the round; open
    /// decisions keep it pending; it is approved when every required approver approved.</para>
    /// </remarks>
    public static ApprovalStatus Evaluate(ApprovalMode mode, IReadOnlyCollection<ApprovalRecordState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count == 0)
        {
            return ApprovalStatus.NotRequested;
        }

        if (mode == ApprovalMode.AnyApprover)
        {
            if (states.Contains(ApprovalRecordState.Approved))
            {
                return ApprovalStatus.Approved;
            }

            if (states.Contains(ApprovalRecordState.Rejected))
            {
                return ApprovalStatus.Rejected;
            }

            return states.Contains(ApprovalRecordState.Requested) ? ApprovalStatus.Requested : ApprovalStatus.Cancelled;
        }

        if (states.Contains(ApprovalRecordState.Rejected))
        {
            return ApprovalStatus.Rejected;
        }

        if (states.Contains(ApprovalRecordState.Cancelled))
        {
            return ApprovalStatus.Cancelled;
        }

        if (states.Contains(ApprovalRecordState.Requested))
        {
            return ApprovalStatus.Requested;
        }

        return states.Contains(ApprovalRecordState.Approved) ? ApprovalStatus.Approved : ApprovalStatus.Cancelled;
    }

    /// <summary>Checks whether a status ends an approval round.</summary>
    /// <param name="status">The status.</param>
    /// <returns><see langword="true"/> for approved and rejected.</returns>
    public static bool IsDecided(ApprovalStatus status) => status is ApprovalStatus.Approved or ApprovalStatus.Rejected;

    /// <summary>Maps the domain status to the public contract state.</summary>
    /// <param name="status">The domain status.</param>
    /// <returns>The contract state.</returns>
    public static ApprovalState ToContract(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Requested => ApprovalState.Requested,
        ApprovalStatus.Approved => ApprovalState.Approved,
        ApprovalStatus.Rejected => ApprovalState.Rejected,
        ApprovalStatus.Cancelled => ApprovalState.Cancelled,
        _ => ApprovalState.NotRequested,
    };

    /// <summary>Maps the public contract policy to the stored mode.</summary>
    /// <param name="policy">The contract policy.</param>
    /// <returns>The domain mode.</returns>
    public static ApprovalMode ToMode(ApprovalPolicy policy) =>
        policy == ApprovalPolicy.AllApprovers ? ApprovalMode.AllApprovers : ApprovalMode.AnyApprover;
}
