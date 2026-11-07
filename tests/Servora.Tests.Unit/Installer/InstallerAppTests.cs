using Servora.Installer.Abstractions;
using Servora.Installer.Cli;
using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="InstallerApp"/> command dispatch.</summary>
public sealed class InstallerAppTests
{
    [Theory]
    [InlineData(nameof(InstallerCommand.Install))]
    [InlineData(nameof(InstallerCommand.Update))]
    [InlineData(nameof(InstallerCommand.Uninstall))]
    [InlineData(nameof(InstallerCommand.Auto))]
    public async Task MachineChangingCommands_RequireElevation(string command)
    {
        var host = new InstallerTestHost();
        host.Environment.IsElevated = false;

        var result = await CreateApp(host, host.Payload("1.0.0")).RunAsync(new InstallerOptions { Command = Enum.Parse<InstallerCommand>(command), Silent = true }, CancellationToken.None);

        Assert.Equal(ExitCode.ElevationRequired, result);
        Assert.Empty(host.Services.Calls);
    }

    [Fact]
    public async Task Status_WorksWithoutElevation()
    {
        var host = new InstallerTestHost();
        host.Environment.IsElevated = false;
        host.SeedInstallation();

        var result = await CreateApp(host, null).RunAsync(new InstallerOptions { Command = InstallerCommand.Status }, CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Contains("DETAIL State: Running", host.Ui.Lines);
        Assert.Contains("DETAIL Installed version: 1.0.0", host.Ui.Lines);
        Assert.Contains(host.Ui.Lines, l => l.StartsWith("DETAIL Health: Healthy (http://localhost:6000/health)", StringComparison.Ordinal));
        Assert.Empty(host.Services.Calls);
        Assert.Null(host.Ui.TranscriptPath);
    }

    [Fact]
    public async Task Status_NotInstalled_ReportsIt()
    {
        var host = new InstallerTestHost();

        var result = await CreateApp(host, null).RunAsync(new InstallerOptions { Command = InstallerCommand.Status }, CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Contains("DETAIL Service: not installed", host.Ui.Lines);
    }

    [Fact]
    public async Task SilentAuto_InstallsWhenMissing_AndUpdatesWhenPresent()
    {
        var host = new InstallerTestHost();
        var app = CreateApp(host, host.Payload("1.0.0"));

        Assert.Equal(ExitCode.Success, await app.RunAsync(new InstallerOptions { Silent = true }, CancellationToken.None));
        Assert.Contains("create", host.Services.Calls);
        Assert.Equal(@"C:\ProgramData\Servora\logs\setup-20261107-143000.log", host.Ui.TranscriptPath);

        var updateApp = CreateApp(host, host.Payload("1.1.0"));
        Assert.Equal(ExitCode.Success, await updateApp.RunAsync(new InstallerOptions { Silent = true }, CancellationToken.None));
        Assert.Equal("1.1.0", host.FileSystem.GetProductVersion(@"C:\Program Files\Servora\Servora.exe"));
        Assert.Single(host.Services.Calls, "create");
    }

    [Fact]
    public async Task MissingPayload_ReturnsPayloadMissing()
    {
        var host = new InstallerTestHost();

        var result = await CreateApp(host, null).RunAsync(new InstallerOptions { Command = InstallerCommand.Install, Silent = true }, CancellationToken.None);

        Assert.Equal(ExitCode.PayloadMissing, result);
    }

    [Fact]
    public async Task InteractiveMenu_ExitReturnsWithoutChanges()
    {
        var host = new InstallerTestHost();
        host.Ui.IsInteractive = true;
        host.Ui.Answers.Enqueue("0");

        var result = await CreateApp(host, host.Payload("1.0.0")).RunAsync(new InstallerOptions(), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Empty(host.Services.Calls);
    }

    [Fact]
    public async Task InteractiveMenu_RunsTheChosenAction()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Ui.IsInteractive = true;
        host.Ui.Answers.Enqueue("1"); // update
        host.Ui.Answers.Enqueue("y"); // confirm update
        host.Ui.Answers.Enqueue("0"); // exit

        var result = await CreateApp(host, host.Payload("1.1.0")).RunAsync(new InstallerOptions(), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal("1.1.0", host.FileSystem.GetProductVersion(@"C:\Program Files\Servora\Servora.exe"));
    }

    private static InstallerApp CreateApp(InstallerTestHost host, IPayloadSource? payload) =>
        new(host.Context, _ => payload, "1.1.0+test");
}
