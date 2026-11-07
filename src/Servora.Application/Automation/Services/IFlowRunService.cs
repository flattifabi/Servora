using Servora.Application.Common;

namespace Servora.Application.Automation.Services;

/// <summary>Execution history use cases: list, inspect, cancel and re-run flow runs.</summary>
public interface IFlowRunService
{
    /// <summary>Lists runs (<c>automation.read</c>).</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of runs.</returns>
    Task<Result<PagedResult<FlowRunListItem>>> ListAsync(FlowRunQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets a run with its timeline (<c>automation.read</c>).</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The detail.</returns>
    Task<Result<FlowRunDetail>> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a queued, running or waiting run and its waiting sub-flows (<c>automation.execute</c> or <c>automation.manage</c>).</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> CancelAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Starts a new run of the same flow with the same trigger data and inputs (<c>automation.execute</c>).</summary>
    /// <param name="runId">The run to repeat.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new run identifier.</returns>
    Task<Result<Guid>> RerunAsync(Guid runId, CancellationToken cancellationToken = default);
}
