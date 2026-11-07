namespace Servora.Application.Automation.Engine;

/// <summary>
/// Hand-off between run producers (triggers, manual starts, the resumer) and the background flow engine worker.
/// Implemented by the infrastructure layer with a bounded in-process channel.
/// </summary>
public interface IFlowRunQueue
{
    /// <summary>Queues a run for execution. Runs that are already queued or executing are ignored.</summary>
    /// <param name="runId">The run identifier.</param>
    /// <returns><see langword="true"/> when the run was queued.</returns>
    bool Enqueue(Guid runId);

    /// <summary>Signals cancellation to a run that is currently executing.</summary>
    /// <param name="runId">The run identifier.</param>
    void SignalCancel(Guid runId);
}
