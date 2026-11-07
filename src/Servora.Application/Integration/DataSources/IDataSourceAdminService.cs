using Servora.Application.Common;

namespace Servora.Application.Integration.DataSources;

/// <summary>
/// Administration of REST data sources, endpoints, credentials, the network allow-list and the execution log.
/// Reads require <c>integration.datasources.read</c>, changes <c>integration.datasources.manage</c>, tests
/// <c>integration.datasources.execute</c>.
/// </summary>
public interface IDataSourceAdminService
{
    /// <summary>Lists data sources.</summary>
    /// <param name="page">Paging and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of data sources.</returns>
    Task<Result<PagedResult<DataSourceListItem>>> ListAsync(PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets key figures.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<Result<DataSourceOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a data source.</summary>
    /// <param name="id">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The configuration.</returns>
    Task<Result<DataSourceModel>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a data source.</summary>
    /// <param name="model">The configuration (empty id creates).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveAsync(DataSourceModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes a data source with its endpoints and credentials.</summary>
    /// <param name="id">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stores the credential of the data source's current authentication type in the secret store.</summary>
    /// <param name="id">The data source.</param>
    /// <param name="value">The API key, token, password or client secret.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetCredentialAsync(Guid id, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes all stored credentials of a data source.</summary>
    /// <param name="id">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ClearCredentialsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists the endpoints of a data source.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The endpoints.</returns>
    Task<Result<IReadOnlyList<DataSourceEndpointModel>>> ListEndpointsAsync(Guid dataSourceId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates an endpoint.</summary>
    /// <param name="model">The endpoint (empty id creates).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveEndpointAsync(DataSourceEndpointModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes an endpoint.</summary>
    /// <param name="endpointId">The endpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteEndpointAsync(Guid endpointId, CancellationToken cancellationToken = default);

    /// <summary>Executes an endpoint with sample parameters and returns the full (bounded) response.</summary>
    /// <param name="dataSourceId">The data source.</param>
    /// <param name="endpointKey">The endpoint key.</param>
    /// <param name="parameters">The parameter values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    Task<Result<DataSourceInvocationResult>> TestEndpointAsync(Guid dataSourceId, string endpointKey, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default);

    /// <summary>Lists execution log entries (newest first).</summary>
    /// <param name="page">Paging.</param>
    /// <param name="dataSourceId">An optional data source filter.</param>
    /// <param name="failuresOnly">Whether only failures are listed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of entries.</returns>
    Task<Result<PagedResult<DataSourceLogItem>>> ListLogsAsync(PageRequest page, Guid? dataSourceId, bool failuresOnly, CancellationToken cancellationToken = default);

    /// <summary>Lists the network allow-list.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries.</returns>
    Task<Result<IReadOnlyList<NetworkAllowListItem>>> ListAllowListAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds an allow-list entry.</summary>
    /// <param name="cidr">The CIDR range or IP address.</param>
    /// <param name="description">The justification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> AddAllowListEntryAsync(string cidr, string? description, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables an allow-list entry.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="enabled">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetAllowListEntryEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Deletes an allow-list entry.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAllowListEntryAsync(Guid id, CancellationToken cancellationToken = default);
}
