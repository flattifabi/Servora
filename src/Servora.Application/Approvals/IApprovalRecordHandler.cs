using Servora.Domain.Approvals;

namespace Servora.Application.Approvals;

/// <summary>
/// Connects a table to the approval engine: describes records for approvers and reacts when an approval round of a
/// record was decided (for example a change moves to <c>Scheduled</c>, a requested item starts fulfilment). Register
/// implementations as scoped services. Other modules (flows, custom tables) can add handlers for their own tables;
/// they can additionally react to <c>sys_approval</c> record events.
/// </summary>
public interface IApprovalRecordHandler
{
    /// <summary>Gets the technical table name handled by this handler.</summary>
    string TableName { get; }

    /// <summary>Describes a record for the approval inbox.</summary>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The description, or <see langword="null"/> when the record does not exist.</returns>
    Task<ApprovalRecordInfo?> DescribeAsync(Guid recordId, CancellationToken cancellationToken);

    /// <summary>
    /// Invoked once when an approval round was decided (<see cref="ApprovalStatus.Approved"/> or
    /// <see cref="ApprovalStatus.Rejected"/>). Runs in the context of the deciding approver, so implementations must not
    /// re-check fulfiller permissions: the engine already verified the approver.
    /// </summary>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="outcome">The outcome of the round.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the record was updated.</returns>
    Task OnDecidedAsync(Guid recordId, ApprovalStatus outcome, CancellationToken cancellationToken);
}
