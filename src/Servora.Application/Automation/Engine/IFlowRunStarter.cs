using Servora.Application.Common;

namespace Servora.Application.Automation.Engine;

/// <summary>
/// Creates flow runs (with idempotency per flow) and queues them. Internal plumbing used by triggers, the scheduler,
/// webhooks, sub-flows and the permission-checked application services; it performs no permission checks itself.
/// </summary>
public interface IFlowRunStarter
{
    /// <summary>Creates and queues a run. A second start with the same idempotency key returns the existing run.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    Task<Result<Guid>> StartAsync(FlowStartRequest request, CancellationToken cancellationToken = default);
}
