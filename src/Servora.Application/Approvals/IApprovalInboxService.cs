using Servora.Application.Common;

namespace Servora.Application.Approvals;

/// <summary>Approver-facing approval operations ("My approvals" page, approvals tab of record pages, portal).</summary>
public interface IApprovalInboxService
{
    /// <summary>Lists approvals of the inbox.</summary>
    /// <param name="filter">The filter. The "all" filters require <c>approval.read</c>.</param>
    /// <param name="page">Paging and search (record number / summary).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of approvals.</returns>
    Task<Result<PagedResult<ApprovalItem>>> ListAsync(ApprovalInboxFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets the key figures of the current user's inbox.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<ApprovalInboxStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Approves an approval assigned to the current user (or on behalf of the approver with <c>approval.manage</c>).</summary>
    /// <param name="approvalId">The approval identifier.</param>
    /// <param name="comment">An optional comment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ApproveAsync(Guid approvalId, string? comment, CancellationToken cancellationToken = default);

    /// <summary>Rejects an approval assigned to the current user (or on behalf of the approver with <c>approval.manage</c>).</summary>
    /// <param name="approvalId">The approval identifier.</param>
    /// <param name="comment">The mandatory reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RejectAsync(Guid approvalId, string comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all approvals (every round) of a record. Allowed with <c>approval.read</c>, the module's read permission or
    /// when the current user is one of the approvers.
    /// </summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The approvals, newest round first.</returns>
    Task<Result<IReadOnlyList<ApprovalItem>>> GetForRecordAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default);
}
