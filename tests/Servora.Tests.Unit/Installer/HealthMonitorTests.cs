using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="HealthMonitor"/>.</summary>
public sealed class HealthMonitorTests
{
    private static readonly Uri HealthUrl = new("http://localhost:5080/health");

    [Fact]
    public async Task WaitAsync_ReturnsHealthyAfterServiceWarmsUp()
    {
        var (services, probe, monitor) = Create();
        var calls = 0;
        probe.Handler = _ => ++calls < 3 ? HealthProbeResult.Unreachable : HealthProbeResult.Healthy;

        var outcome = await monitor.WaitAsync("Servora", HealthUrl, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1), false, CancellationToken.None);

        Assert.Equal(HealthCheckOutcome.Healthy, outcome);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task WaitAsync_DetectsStoppedService()
    {
        var (services, _, monitor) = Create();
        services.Service = services.Service! with { State = ServiceState.Stopped };

        var outcome = await monitor.WaitAsync("Servora", HealthUrl, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1), false, CancellationToken.None);

        Assert.Equal(HealthCheckOutcome.ServiceStopped, outcome);
    }

    [Fact]
    public async Task WaitAsync_TimesOut_AndTreatsUnhealthyAsFailureUnlessAccepted()
    {
        var (_, probe, monitor) = Create();
        probe.Handler = _ => HealthProbeResult.Unhealthy;

        Assert.Equal(
            HealthCheckOutcome.TimedOut,
            await monitor.WaitAsync("Servora", HealthUrl, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(5), false, CancellationToken.None));
        Assert.Equal(
            HealthCheckOutcome.Healthy,
            await monitor.WaitAsync("Servora", HealthUrl, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(5), true, CancellationToken.None));
    }

    private static (FakeServiceManager Services, FakeHealthProbe Probe, HealthMonitor Monitor) Create()
    {
        var services = new FakeServiceManager();
        services.Install(@"C:\Program Files\Servora\Servora.exe");
        var probe = new FakeHealthProbe();
        return (services, probe, new HealthMonitor(probe, services, TimeProvider.System));
    }
}
