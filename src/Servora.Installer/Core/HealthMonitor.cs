using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>Polls the health endpoint until it succeeds, the service stops or the timeout elapses.</summary>
/// <param name="probe">The health probe.</param>
/// <param name="services">The service manager (used to detect a crashed service early).</param>
/// <param name="time">The clock.</param>
internal sealed class HealthMonitor(IHealthProbe probe, IServiceManager services, TimeProvider time)
{
    /// <summary>Waits for the service to become healthy.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="healthUrl">The health URL.</param>
    /// <param name="timeout">The maximum wait time.</param>
    /// <param name="interval">The delay between probes.</param>
    /// <param name="acceptAnyResponse">
    /// When <see langword="true"/>, any HTTP response counts as success. Used for fresh installations, where
    /// dependency health checks may legitimately report "unhealthy" until the setup wizard has configured a database.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<HealthCheckOutcome> WaitAsync(
        string serviceName,
        Uri healthUrl,
        TimeSpan timeout,
        TimeSpan interval,
        bool acceptAnyResponse,
        CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        while (true)
        {
            var state = services.GetState(serviceName);
            if (state is ServiceState.Stopped or ServiceState.NotInstalled)
            {
                return HealthCheckOutcome.ServiceStopped;
            }

            var result = await probe.ProbeAsync(healthUrl, cancellationToken).ConfigureAwait(false);
            if (result == HealthProbeResult.Healthy || (acceptAnyResponse && result == HealthProbeResult.Unhealthy))
            {
                return HealthCheckOutcome.Healthy;
            }

            if (time.GetElapsedTime(started) >= timeout)
            {
                return HealthCheckOutcome.TimedOut;
            }

            if (interval > TimeSpan.Zero)
            {
                await Task.Delay(interval, time, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
