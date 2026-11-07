using Servora.Installer.Cli;
using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="InstallWorkflow"/>.</summary>
public sealed class InstallWorkflowTests
{
    private const string Install = InstallerTestHost.InstallDirectory;
    private const string Data = InstallerTestHost.DataDirectory;

    [Fact]
    public async Task Install_FreshMachine_RegistersServiceAndWritesInstanceSettings()
    {
        var host = new InstallerTestHost();

        var result = await new InstallWorkflow(host.Context).RunAsync(
            new InstallerOptions { Silent = true, Port = 8080, Firewall = true }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);

        // Program files copied from the payload.
        Assert.Equal("new", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
        Assert.Equal("1.0.0", host.FileSystem.GetProductVersion(Path.Combine(Install, "Servora.exe")));

        // Service registered with the quoted executable path and the least-privilege account, then started.
        var created = host.Services.Created!;
        Assert.Equal("Servora", created.Name);
        Assert.Equal(Path.Combine(Install, "Servora.exe"), created.ExecutablePath);
        Assert.Equal(ServiceAccountKind.Virtual, created.Account);
        Assert.Empty(created.Environment);
        Assert.Equal(["create", "start"], host.Services.Calls);
        Assert.Equal(ServiceState.Running, host.Services.Service!.State);

        // Instance settings with the chosen port.
        Assert.Contains("http://0.0.0.0:8080", host.FileSystem.Content(Path.Combine(Data, "appsettings.json")), StringComparison.Ordinal);

        // Directory ACLs hardened (install dir first, data dir after the service account exists).
        var icacls = host.Processes.RunsOf("icacls.exe");
        Assert.Equal(2, icacls.Count);
        Assert.Equal(Install, icacls[0][0]);
        Assert.Equal(Data, icacls[1][0]);
        Assert.Contains(@"NT SERVICE\Servora:(OI)(CI)M", icacls[1]);
        Assert.All(host.Processes.Runs, r => Assert.StartsWith(@"C:\Windows\System32\", r.FileName, StringComparison.OrdinalIgnoreCase));

        // Firewall rule for the port only, domain/private profiles.
        var netsh = host.Processes.RunsOf("netsh.exe");
        Assert.Contains(netsh, args => args.Contains("add") && args.Contains("localport=8080") && args.Contains("profile=domain,private"));

        // Event source, health check, no browser in silent mode.
        Assert.Equal(Install, host.EventLog.Registered["Servora"]);
        Assert.Equal("http://localhost:8080/health", host.Probe.Probed[0].ToString());
        Assert.Empty(host.Processes.OpenedUrls);
    }

    [Fact]
    public async Task Install_Interactive_AsksAndOpensSetupWizard()
    {
        var host = new InstallerTestHost();
        host.Ui.IsInteractive = true;
        foreach (var answer in new[] { "", "8081", "n", "y" })
        {
            host.Ui.Answers.Enqueue(answer); // install dir (default), port, firewall, confirm
        }

        var result = await new InstallWorkflow(host.Context).RunAsync(new InstallerOptions(), host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal("http://localhost:8081/setup", Assert.Single(host.Processes.OpenedUrls).ToString());
        Assert.Empty(host.Processes.RunsOf("netsh.exe"));
    }

    [Fact]
    public async Task Install_AlreadyInstalled_IsRejected()
    {
        var host = new InstallerTestHost();
        host.SeedInstallation();

        var result = await new InstallWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.1.0"), CancellationToken.None);

        Assert.Equal(ExitCode.AlreadyInstalled, result);
        Assert.Equal("old", host.FileSystem.Content(Path.Combine(Install, "Servora.dll")));
    }

    [Fact]
    public async Task Install_ServiceNeverResponds_RevertsEverythingButData()
    {
        var host = new InstallerTestHost();
        host.Probe.Handler = _ => HealthProbeResult.Unreachable;

        var result = await new InstallWorkflow(host.Context).RunAsync(
            new InstallerOptions { Silent = true, Firewall = true }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Failed, result);
        Assert.Null(host.Services.Service);
        Assert.Contains("delete", host.Services.Calls);
        Assert.False(host.FileSystem.DirectoryExists(Install));
        Assert.Empty(host.EventLog.Registered);
        Assert.Contains(host.Processes.RunsOf("netsh.exe"), args => args.Contains("delete"));
        Assert.True(host.FileSystem.FileExists(Path.Combine(Data, "appsettings.json")));
    }

    [Fact]
    public async Task Install_UnconfiguredInstanceReportingUnhealthy_StillSucceeds()
    {
        var host = new InstallerTestHost();
        host.Probe.Handler = _ => HealthProbeResult.Unhealthy;

        var result = await new InstallWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
    }

    [Fact]
    public async Task Install_PortInUse_FailsInSilentMode_UnlessForced()
    {
        var host = new InstallerTestHost();
        host.Ports.Occupied.Add(5080);

        var refused = await new InstallWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.0.0"), CancellationToken.None);
        Assert.Equal(ExitCode.Failed, refused);
        Assert.Empty(host.Services.Calls);

        var forced = await new InstallWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true, Force = true }, host.Payload("1.0.0"), CancellationToken.None);
        Assert.Equal(ExitCode.Success, forced);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"relative\path")]
    [InlineData(@"C:\Windows\Servora")]
    [InlineData(@"C:\ProgramData\Servora\bin")]
    [InlineData(@"C:\Program Files")]
    public async Task Install_InvalidInstallDirectory_IsRejected(string installDirectory)
    {
        var host = new InstallerTestHost();

        var result = await new InstallWorkflow(host.Context).RunAsync(
            new InstallerOptions { Silent = true, InstallDirectory = installDirectory }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.InvalidArguments, result);
        Assert.Empty(host.Services.Calls);
    }

    [Fact]
    public async Task Install_IntoFolderWithForeignFiles_IsRejected()
    {
        var host = new InstallerTestHost();
        host.FileSystem.AddFile(@"D:\Apps\Other\tool.exe", "x");

        var result = await new InstallWorkflow(host.Context).RunAsync(
            new InstallerOptions { Silent = true, InstallDirectory = @"D:\Apps\Other" }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.InvalidArguments, result);
        Assert.Equal("x", host.FileSystem.Content(@"D:\Apps\Other\tool.exe"));
    }

    [Fact]
    public async Task Install_ExistingInstanceSettings_AreKeptWhenPortUnchanged()
    {
        var host = new InstallerTestHost();
        const string existing = """{ "Urls": "http://0.0.0.0:5080", "Kestrel": { "Certificates": { "Default": { "Path": "C:\\certs\\servora.pfx" } } } }""";
        host.FileSystem.AddFile(Path.Combine(Data, "appsettings.json"), existing);

        var result = await new InstallWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal(existing, host.FileSystem.Content(Path.Combine(Data, "appsettings.json")));
    }

    [Fact]
    public async Task Install_NewPort_UpdatesUrlsAndPreservesOtherSettings()
    {
        var host = new InstallerTestHost();
        host.FileSystem.AddFile(Path.Combine(Data, "appsettings.json"), """{ "urls": "http://0.0.0.0:5080", "Custom": { "Flag": true } }""");

        var result = await new InstallWorkflow(host.Context).RunAsync(new InstallerOptions { Silent = true, Port = 9000 }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        var content = host.FileSystem.Content(Path.Combine(Data, "appsettings.json"))!;
        Assert.Contains("http://0.0.0.0:9000", content, StringComparison.Ordinal);
        Assert.Contains("\"Flag\": true", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\"urls\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_CustomDataDirectory_IsPassedToTheService()
    {
        var host = new InstallerTestHost();

        var result = await new InstallWorkflow(host.Context).RunAsync(
            new InstallerOptions { Silent = true, DataDirectory = @"D:\ServoraData", ServiceAccount = ServiceAccountKind.LocalSystem }, host.Payload("1.0.0"), CancellationToken.None);

        Assert.Equal(ExitCode.Success, result);
        Assert.Equal(@"D:\ServoraData", host.Services.Created!.Environment["SERVORA_DATA_DIR"]);
        Assert.True(host.FileSystem.FileExists(@"D:\ServoraData\appsettings.json"));
        var dataAcl = host.Processes.RunsOf("icacls.exe")[1];
        Assert.Equal(@"D:\ServoraData", dataAcl[0]);
        Assert.DoesNotContain(dataAcl, a => a.Contains("NT SERVICE", StringComparison.Ordinal));
    }
}
