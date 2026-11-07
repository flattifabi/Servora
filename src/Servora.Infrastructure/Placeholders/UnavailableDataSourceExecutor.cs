using Servora.Application.Common;
using Servora.Application.Integration;

namespace Servora.Infrastructure.Placeholders;

/// <summary>Placeholder <see cref="IDataSourceExecutor"/> until the Automation and Integration module (T-08) is registered.</summary>
/// <remarks>Placeholder registered by the core kernel so every module can run in isolation. The owning module registers the real implementation later in the DI container, which takes precedence.</remarks>
public sealed class UnavailableDataSourceExecutor : IDataSourceExecutor
{
    /// <inheritdoc />
    public Task<IReadOnlyList<DataSourceSummary>> ListAccessibleAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DataSourceSummary>>([]);

    /// <inheritdoc />
    public Task<Result<DataSourceResult>> ExecuteAsync(Guid dataSourceId, string endpointKey, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<DataSourceResult>.Failure("Data sources are provided by the Integration module, which is not registered."));
}
