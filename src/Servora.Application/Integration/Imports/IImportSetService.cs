using Servora.Application.Common;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.Imports;

/// <summary>Import set use cases. Every method requires <c>integration.imports.manage</c>.</summary>
public interface IImportSetService
{
    /// <summary>Lists import sets.</summary>
    /// <param name="page">Paging and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of import sets.</returns>
    Task<Result<PagedResult<ImportSetListItem>>> ListAsync(PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets key figures.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<Result<ImportOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets an import set.</summary>
    /// <param name="id">The import set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition.</returns>
    Task<Result<ImportSetModel>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates an import set.</summary>
    /// <param name="model">The definition (empty id creates).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveAsync(ImportSetModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes an import set with its run history.</summary>
    /// <param name="id">The import set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Runs a data source import now.</summary>
    /// <param name="id">The import set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    Task<Result<Guid>> RunNowAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stages and transforms an uploaded CSV file.</summary>
    /// <param name="id">The import set.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="content">The file content (at most <see cref="ImportProcessor.MaxCsvBytes"/> bytes are read).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    Task<Result<Guid>> RunCsvAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Parses the first rows of a CSV file to help building the transform map.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The headers and up to 5 rows.</returns>
    Task<Result<CsvDocument>> PreviewCsvAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>Lists the runs of an import set (newest first).</summary>
    /// <param name="id">The import set.</param>
    /// <param name="page">Paging.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of runs.</returns>
    Task<Result<PagedResult<ImportRunItem>>> ListRunsAsync(Guid id, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Lists the rows of a run.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="status">An optional status filter.</param>
    /// <param name="page">Paging.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of rows.</returns>
    Task<Result<PagedResult<ImportRowItem>>> ListRowsAsync(Guid runId, ImportRowStatus? status, PageRequest page, CancellationToken cancellationToken = default);
}
