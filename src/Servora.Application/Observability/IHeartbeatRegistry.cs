namespace Servora.Application.Observability;

/// <summary>
/// Liveness registry for background services (singleton). Hosted services call <see cref="Beat"/> once per loop; the
/// health check reports services whose heartbeat is overdue.
/// </summary>
public interface IHeartbeatRegistry
{
    /// <summary>Records a heartbeat.</summary>
    /// <param name="name">The service name (stable, human readable).</param>
    /// <param name="expectedInterval">The interval in which the next heartbeat is expected.</param>
    /// <param name="status">An optional short status text.</param>
    void Beat(string name, TimeSpan expectedInterval, string? status = null);

    /// <summary>Gets all registered heartbeats ordered by name.</summary>
    /// <returns>The heartbeats.</returns>
    IReadOnlyList<HeartbeatInfo> GetAll();
}
