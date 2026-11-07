using Servora.Application.Common;

namespace Servora.Application.Automation;

/// <summary>
/// Starts and inspects flow executions. Implemented by the Automation module (T-08); used by business rules (T-07),
/// the REST API and MCP tools (T-10).
/// </summary>
public interface IFlowExecutionService
{
    /// <summary>Lists flows that the current actor may start manually.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The flows.</returns>
    Task<IReadOnlyList<FlowSummary>> ListStartableAsync(CancellationToken cancellationToken = default);

    /// <summary>Queues a flow execution. Permission automation.execute (or a system actor) is required.</summary>
    /// <param name="flowId">The flow identifier.</param>
    /// <param name="inputs">Input values (bounded, validated against the flow inputs).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    Task<Result<Guid>> StartAsync(Guid flowId, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default);

    /// <summary>Gets the status of a run.</summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The status, or not found.</returns>
    Task<Result<FlowRunSummary>> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
}
