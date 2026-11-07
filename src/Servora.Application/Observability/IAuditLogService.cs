using Servora.Application.Common;
using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>
/// Audit log viewer use cases: filtering, details, CSV export, hash-chain verification, statistics and retention.
/// Requires <c>admin.audit.read</c>; changing the retention additionally requires <c>admin.settings.manage</c>.
/// </summary>
public interface IAuditLogService
{
    /// <summary>Queries audit events (default: newest first).</summary>
    /// <param name="query">The filter.</param>
    /// <param name="page">Paging and sorting (sort keys: OccurredAt, Sequence, Category, Action, ActorName, Outcome).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<AuditEvent>>> QueryAsync(AuditQuery query, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets one audit event.</summary>
    /// <param name="id">The event identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The event.</returns>
    Task<Result<AuditEvent>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Exports the filtered events as CSV (at most <c>50,000</c> rows, newest first). The export is audited.</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The UTF-8 CSV document (with BOM for spreadsheet compatibility).</returns>
    Task<Result<byte[]>> ExportCsvAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>Verifies the complete hash chain. The result is stored and audited.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verification result.</returns>
    Task<Result<AuditChainVerification>> VerifyChainAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the result of the most recent verification, if any.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The last result or <see langword="null"/>.</returns>
    Task<Result<AuditChainVerification?>> GetLastVerificationAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets KPIs and breakdowns.</summary>
    /// <param name="days">The statistics period in days (1..90).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<AuditStatistics>> GetStatisticsAsync(int days = 14, CancellationToken cancellationToken = default);

    /// <summary>Gets the distinct categories and actions for filter drop-downs.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Categories and actions (bounded).</returns>
    Task<Result<AuditFacets>> GetFacetsAsync(CancellationToken cancellationToken = default);

    /// <summary>Changes the audit retention (0 = keep forever, otherwise 30..3650 days).</summary>
    /// <param name="days">The retention in days.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetRetentionDaysAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>Deletes events older than the retention period in bounded batches, keeping the chain anchor (background; system actor).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted events.</returns>
    Task<int> PruneAsync(CancellationToken cancellationToken = default);
}
