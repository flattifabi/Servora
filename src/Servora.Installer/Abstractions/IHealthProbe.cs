using Servora.Installer.Model;

namespace Servora.Installer.Abstractions;

/// <summary>Performs a single request against the Servora <c>/health</c> endpoint. Faked in unit tests.</summary>
internal interface IHealthProbe
{
    /// <summary>Requests the health endpoint once.</summary>
    /// <param name="healthUrl">The absolute health URL.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The probe result.</returns>
    Task<HealthProbeResult> ProbeAsync(Uri healthUrl, CancellationToken cancellationToken);
}
