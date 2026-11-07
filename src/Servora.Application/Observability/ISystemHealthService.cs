using Servora.Application.Common;

namespace Servora.Application.Observability;

/// <summary>Runs the registered health checks and collects process and request metrics. Requires <c>admin.health.read</c>.</summary>
public interface ISystemHealthService
{
    /// <summary>Builds the health report.</summary>
    /// <param name="metricsWindow">The request metrics window (default one hour, at most 24 hours).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The report.</returns>
    Task<Result<SystemHealthReport>> GetReportAsync(TimeSpan? metricsWindow = null, CancellationToken cancellationToken = default);
}
