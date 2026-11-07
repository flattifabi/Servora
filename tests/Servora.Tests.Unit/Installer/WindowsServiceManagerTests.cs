using Servora.Installer.Model;
using Servora.Installer.Platform;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>
/// Read-only checks of <see cref="WindowsServiceManager"/> against the real Service Control Manager. They query a
/// built-in Windows service and never create, start, stop or delete anything.
/// </summary>
public sealed class WindowsServiceManagerTests
{
    [Fact]
    public void Query_BuiltInService_ReturnsConfigurationWithoutElevation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var manager = new WindowsServiceManager(new FakeProcessRunner(), new WindowsInstallerEnvironment());

        // The Windows Event Log service exists on every supported Windows edition.
        var info = manager.Query("EventLog");

        Assert.NotNull(info);
        Assert.Equal("EventLog", info.Name, ignoreCase: true);
        Assert.Contains("svchost.exe", info.BinaryPath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(ServiceState.NotInstalled, manager.GetState("EventLog"));
    }

    [Fact]
    public void Query_UnknownService_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var manager = new WindowsServiceManager(new FakeProcessRunner(), new WindowsInstallerEnvironment());

        Assert.Null(manager.Query("Servora-Test-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(ServiceState.NotInstalled, manager.GetState("Servora-Test-" + Guid.NewGuid().ToString("N")));
    }
}
