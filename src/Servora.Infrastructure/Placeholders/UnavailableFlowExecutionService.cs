using Servora.Application.Automation;
using Servora.Application.Common;

namespace Servora.Infrastructure.Placeholders;

/// <summary>Placeholder <see cref="IFlowExecutionService"/> until the Automation module (T-08) is registered.</summary>
/// <remarks>Placeholder registered by the core kernel so every module can run in isolation. The owning module registers the real implementation later in the DI container, which takes precedence.</remarks>
public sealed class UnavailableFlowExecutionService : IFlowExecutionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<FlowSummary>> ListStartableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FlowSummary>>([]);

    /// <inheritdoc />
    public Task<Result<Guid>> StartAsync(Guid flowId, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<Guid>.Failure("Flows are provided by the Automation module, which is not registered."));

    /// <inheritdoc />
    public Task<Result<FlowRunSummary>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<FlowRunSummary>.NotFound());
}
