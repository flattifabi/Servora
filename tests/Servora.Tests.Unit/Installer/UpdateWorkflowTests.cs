using Servora.Installer.Cli;
using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="UpdateWorkflow"/>: backup, replacement, health check and automatic rollback.</summary>
public sealed class UpdateWorkflowTests
{
    private const string Install = InstallerTestHost.InstallDirectory;
    private const string Data = InstallerTestHost.DataDirectory;

    [Fact]
    public async Task Update_ReplacesBinaries_KeepsDataAndBacksUp()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        var dataBefore = DataWithoutBackupsAndLogs(host);

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal(["stop", "start"], host.Services.Calls);
        Assert.Equal(ServiceState.Running, host.Services.Service!.State);

        // New binaries in place, stale files removed.
        Assert.Equal("new", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
        Assert.Equal("new", host.FileSystem.Content(Path.Combine(Install, @"wwwroot\css\app.css")));
        Assert.Equal("1.1.0", host.FileSystem.GetProductVersion(Path.Combine(Install, "Servora.exe")));
        Assert.False(host.FileSystem.FileExists(Path.Combine(Install, "Obsolete.dll")));

        // Data directory and instance appsettings untouched.
        Assert.Equal(dataBefore, DataWithoutBackupsAndLogs(host));

        // Backup of the previous binaries.
        var backup = Path.Combine(InstallerTestHost.BackupDirectory, "20261107-143000-v1.0.0");
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(backup, "Servora.dll")));
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(backup, "Obsolete.dll")));
        Assert.Equal("1.0.0", host.FileSystem.GetProductVersion(Path.Combine(backup, "Servora.exe")));

        // The health check uses the port from the instance settings.
        Assert.All(host.Probe.Probed, u => Assert.Equal("http://localhost:6000/health", u.ToString()));

        // Staging directory cleaned up.
        Assert.DoesNotContain(host.FileSystem.AllFiles, f => f.StartsWith(@"C:\Temp\", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_HealthCheckFails_RollsBackToPreviousVersion()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.OnlyOldVersionIsHealthy();

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.RolledBack, result);
        Assert.Equal(["stop", "start", "stop", "start"], host.Services.Calls);
        Assert.Equal(ServiceState.Running, host.Services.Service!.State);
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Obsolete.dll")));
        Assert.Equal("1.0.0", host.FileSystem.GetProductVersion(Path.Combine(Install, "Servora.exe")));
        Assert.Contains(host.Ui.Lines, l => l.Contains("Rolled back to version 1.0.0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Update_ServiceDoesNotStart_RollsBack()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Services.StartFailures = 1;

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.RolledBack, result);
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
        Assert.Equal(ServiceState.Running, host.Services.Service!.State);
    }

    [Fact]
    public async Task Update_CopyFailsHalfway_RollsBack()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.FileSystem.FailCopy = (source, destination) =>
            source.StartsWith(@"C:\Temp\", StringComparison.OrdinalIgnoreCase) && destination.EndsWith("app.css", StringComparison.OrdinalIgnoreCase);

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.RolledBack, result);
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, @"wwwroot\css\app.css")));
        Assert.Equal(ServiceState.Running, host.Services.Service!.State);
    }

    [Fact]
    public async Task Update_StopFails_ChangesNothing()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Services.FailStop = true;

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Failed, result);
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
        Assert.False(host.FileSystem.DirectoryExists(InstallerTestHost.BackupDirectory));
    }

    [Fact]
    public async Task Update_Downgrade_IsRefusedWithoutForce()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("0.9.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Failed, result);
        Assert.Empty(host.Services.Calls);
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
    }

    [Fact]
    public async Task Update_Downgrade_IsAllowedWithForce()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true, Force = true }, host.Payload("0.9.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal("0.9.0", host.FileSystem.GetProductVersion(Path.Combine(Install, "Servora.exe")));
    }

    [Fact]
    public async Task Update_KeepsOnlyConfiguredNumberOfBackups()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        foreach (var stamp in new[] { "20260101-100000-v0.7.0", "20260201-100000-v0.8.0", "20260301-100000-v0.9.0" })
        {
            host.FileSystem.AddFile(Path.Combine(InstallerTestHost.BackupDirectory, stamp, "Servora.dll"), stamp);
        }

        host.FileSystem.AddFile(Path.Combine(InstallerTestHost.BackupDirectory, "manual-copy", "readme.txt"), "keep");

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true, KeepBackups = 2 }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        var remaining = host.FileSystem.GetDirectories(InstallerTestHost.BackupDirectory).Select(Path.GetFileName).ToList();
        Assert.Equal(["20260301-100000-v0.9.0", "20261107-143000-v1.0.0", "manual-copy"], remaining.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Update_PreservesAppDataInsideInstallDirectory()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.FileSystem.AddFile(Path.Combine(Install, @"App_Data\servora.db"), "local data");

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal("local data", host.FileSystem.Content(Path.Combine(Install, @"App_Data\servora.db")));
        Assert.False(host.FileSystem.FileExists(Path.Combine(InstallerTestHost.BackupDirectory, "20261107-143000-v1.0.0", @"App_Data\servora.db")));
    }

    [Fact]
    public async Task Update_UsesServiceDataDirectoryOverride()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Services.Environment["SERVORA_DATA_DIR"] = @"D:\ServoraData";
        host.FileSystem.AddFile(@"D:\ServoraData\appsettings.json", """{ "Urls": "http://+:7000" }""");

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.All(host.Probe.Probed, u => Assert.Equal("http://localhost:7000/health", u.ToString()));
        Assert.Equal("""{ "Urls": "http://+:7000" }""", host.FileSystem.Content(@"D:\ServoraData\appsettings.json"));
    }

    [Fact]
    public async Task Update_NotInstalled_ReturnsNotInstalled()
    {
        var host = new InstallerTestHost();

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.NotInstalled, result);
    }

    [Fact]
    public async Task Update_InvalidPayload_ChangesNothing()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        var payload = host.Payload("1.1.0");
        payload.OmitExecutable = true;

        var error = await Assert.ThrowsAsync<InstallerException>(() => new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, payload, CancellationToken.None));

        Assert.Equal(ExitCode.PayloadMissing, error.ExitCode);
        Assert.Empty(host.Services.Calls);
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
    }

    [Fact]
    public async Task Update_InteractiveDecline_IsCancelled()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Ui.IsInteractive = true;
        host.Ui.Answers.Enqueue("n");

        var result = await new UpdateWorkflow(host.Context).RunAsync(new InstallerOptions(), host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Cancelled, result);
        Assert.Empty(host.Services.Calls);
    }

    private static IReadOnlyDictionary<string, string> DataWithoutBackupsAndLogs(InstallerTestHost host) =>
        host.FileSystem.Snapshot(Data)
            .Where(p => !p.Key.StartsWith("backups", StringComparison.OrdinalIgnoreCase) && !p.Key.StartsWith("logs", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(p => p.Key, p => p.Value);
}
