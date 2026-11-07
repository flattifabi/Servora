using Servora.Application.Common;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.DataSources;

/// <summary>
/// Low-level HTTP execution of a data source endpoint: request building, authentication from the secret store, SSRF
/// protection (DNS validation, connect-time IP checks, redirect re-validation), timeouts, retries and size limits.
/// Implemented by the infrastructure layer. Performs no permission checks – only call it from permission-checked services.
/// </summary>
public interface IDataSourceInvoker
{
    /// <summary>Executes an endpoint.</summary>
    /// <param name="source">The data source.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="parameters">The parameter values (validated against the endpoint definition).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, or a failure for validation, SSRF, timeout or network errors.</returns>
    Task<Result<DataSourceInvocationResult>> InvokeAsync(DataSource source, DataSourceEndpoint endpoint, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default);
}
