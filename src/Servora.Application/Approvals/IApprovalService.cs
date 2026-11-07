using Servora.Application.Common;

namespace Servora.Application.Approvals;

/// <summary>
/// Generic approval engine for any record. Implemented by the Service Management module (T-05); used by change
/// management, the service catalog, flows (T-08) and the portal (T-11).
/// </summary>
public interface IApprovalService
{
    /// <summary>Creates approval records for the approvers and notifies them.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the aggregated approval state of a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The summary.</returns>
    Task<ApprovalSummary> GetSummaryAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>Cancels all pending approvals of a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CancelAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default);
}
