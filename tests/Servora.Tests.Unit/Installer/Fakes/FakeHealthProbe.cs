using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Health probe whose answer is computed by a delegate (healthy by default).</summary>
internal sealed class FakeHealthProbe : IHealthProbe
{
    /// <summary>Gets or sets the result handler.</summary>
    public Func<Uri, HealthProbeResult> Handler { get; set; } = _ => HealthProbeResult.Healthy;

    /// <summary>Gets the probed URLs.</summary>
    public List<Uri> Probed { get; } = [];

    /// <inheritdoc />
    public Task<HealthProbeResult> ProbeAsync(Uri healthUrl, CancellationToken cancellationToken)
    {
        Probed.Add(healthUrl);
        return Task.FromResult(Handler(healthUrl));
    }
}
