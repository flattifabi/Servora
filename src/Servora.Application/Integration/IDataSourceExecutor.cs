using Servora.Application.Common;

namespace Servora.Application.Integration;

/// <summary>
/// Executes approved endpoints of configured HTTP data sources with SSRF protection, timeouts and size limits.
/// Implemented by the Automation and Integration module (T-08); used by flows and MCP tools (T-10).
/// Never exposes arbitrary URL execution.
/// </summary>
public interface IDataSourceExecutor
{
    /// <summary>Lists data sources and endpoints the current actor may execute.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data sources.</returns>
    Task<IReadOnlyList<DataSourceSummary>> ListAccessibleAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes an approved endpoint.</summary>
    /// <param name="dataSourceId">The data source identifier.</param>
    /// <param name="endpointKey">The endpoint key.</param>
    /// <param name="parameters">The parameter values (validated and encoded by the executor).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded result.</returns>
    Task<Result<DataSourceResult>> ExecuteAsync(Guid dataSourceId, string endpointKey, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default);
}
