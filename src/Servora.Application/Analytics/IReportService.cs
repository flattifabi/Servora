using Servora.Application.Common;
using Servora.Domain.Analytics;

namespace Servora.Application.Analytics;

/// <summary>
/// Report builder use cases. Viewing and running reports requires <c>analytics.read</c> plus read access to the source
/// table; creating, editing, sharing and deleting requires <c>analytics.reports.manage</c> (only the owner edits).
/// </summary>
public interface IReportService
{
    /// <summary>Lists the tables the current user can report on.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tables with usable fields.</returns>
    Task<Result<IReadOnlyList<ReportTableInfo>>> GetTablesAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists visible saved reports.</summary>
    /// <param name="scope">Which reports.</param>
    /// <param name="page">Paging and search (sort keys: Name, UpdatedAt, TableName).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<ReportSummary>>> ListAsync(ReportListScope scope, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets a visible saved report.</summary>
    /// <param name="id">The report id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The report.</returns>
    Task<Result<ReportDefinition>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a report.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The report id.</returns>
    Task<Result<Guid>> SaveAsync(ReportSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a report owned by the current user (and every pin of it).</summary>
    /// <param name="id">The report id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Runs an unsaved specification (report builder preview).</summary>
    /// <param name="spec">The specification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result<ReportResult>> RunAsync(ReportSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Runs a visible saved report.</summary>
    /// <param name="id">The report id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result<ReportResult>> RunSavedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Pins a visible report to the personal dashboard.</summary>
    /// <param name="id">The report id.</param>
    /// <param name="wide">Whether the tile spans the full width.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> PinAsync(Guid id, bool wide = false, CancellationToken cancellationToken = default);

    /// <summary>Removes a report from the personal dashboard.</summary>
    /// <param name="id">The report id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UnpinAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the pinned reports of the current user with their results (at most 12).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pinned reports.</returns>
    Task<Result<IReadOnlyList<PinnedReport>>> GetPinnedAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists role names a report can be shared with.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Role names and labels.</returns>
    Task<Result<IReadOnlyList<ShareableRole>>> GetShareableRolesAsync(CancellationToken cancellationToken = default);
}
