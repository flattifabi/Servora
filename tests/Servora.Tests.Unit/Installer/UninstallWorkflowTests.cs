using Servora.Installer.Cli;
using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="UninstallWorkflow"/>.</summary>
public sealed class UninstallWorkflowTests
{
    private const string Install = InstallerTestHost.InstallDirectory;
    private const string Data = InstallerTestHost.DataDirectory;

    [Fact]
    public void Uninstall_KeepData_RemovesServiceAndProgramFilesOnly()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.EventLog.Registered["Servora"] = Install;

        var result = new UninstallWorkflow(host.Context).Run(new InstallerOptions { Silent = true, KeepData = true });

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal(["stop", "delete"], host.Services.Calls);
        Assert.Null(host.Services.Service);
        Assert.False(host.FileSystem.DirectoryExists(Install));
        Assert.True(host.FileSystem.FileExists(Path.Combine(Data, "servora.db")));
        Assert.True(host.FileSystem.FileExists(Path.Combine(Data, @"keys\key-1.xml")));
        Assert.Empty(host.EventLog.Registered);
        Assert.Contains(host.Processes.RunsOf("netsh.exe"), args => args.Contains("delete"));
    }

    [Fact]
    public void Uninstall_Silent_WithoutKeepData_DeletesDataDirectory()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();

        var result = new UninstallWorkflow(host.Context).Run(new InstallerOptions { Silent = true });

        Assert.Equal(ExitCode.Success, result);
        Assert.False(host.FileSystem.DirectoryExists(Data));
        Assert.False(host.FileSystem.DirectoryExists(Install));
    }

    [Fact]
    public void Uninstall_CustomDataDirectory_IsResolvedFromTheServiceBeforeDeletion()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Services.Environment["SERVORA_DATA_DIR"] = @"D:\ServoraData";
        host.FileSystem.AddFile(@"D:\ServoraData\servora.db", "db");

        var result = new UninstallWorkflow(host.Context).Run(new InstallerOptions { Silent = true });

        Assert.Equal(ExitCode.Success, result);
        Assert.False(host.FileSystem.DirectoryExists(@"D:\ServoraData"));
    }

    [Fact]
    public void Uninstall_Interactive_DefaultsToKeepingData()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Ui.IsInteractive = true;
        host.Ui.Answers.Enqueue("y"); // remove service and program files
        host.Ui.Answers.Enqueue("");  // delete data? -> default (no)

        var result = new UninstallWorkflow(host.Context).Run(new InstallerOptions());

        Assert.Equal(ExitCode.Success, result);
        Assert.False(host.FileSystem.DirectoryExists(Install));
        Assert.True(host.FileSystem.FileExists(Path.Combine(Data, "servora.db")));
    }

    [Fact]
    public void Uninstall_InteractiveDecline_ChangesNothing()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Ui.IsInteractive = true;
        host.Ui.Answers.Enqueue("n");

        var result = new UninstallWorkflow(host.Context).Run(new InstallerOptions());

        Assert.Equal(ExitCode.Cancelled, result);
        Assert.Empty(host.Services.Calls);
        Assert.True(host.FileSystem.DirectoryExists(Install));
    }

    [Fact]
    public void Uninstall_StopFails_RemovesNothing()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();
        host.Services.FailStop = true;

        var result = new UninstallWorkflow(host.Context).Run(new InstallerOptions { Silent = true });

        Assert.Equal(ExitCode.Failed, result);
        Assert.NotNull(host.Services.Service);
        Assert.True(host.FileSystem.DirectoryExists(Install));
    }

    [Fact]
    public void Uninstall_NotInstalled_ReturnsNotInstalled()
    {
        var host = new InstallerTestHost();

        Assert.Equal(ExitCode.NotInstalled, new UninstallWorkflow(host.Context).Run(new InstallerOptions { Silent = true }));
    }
}
