using Servora.Installer.Core;
using Servora.Installer.Model;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Wires the installer workflows to fakes and provides standard machine states.</summary>
internal sealed class InstallerTestHost
{
    /// <summary>Default install directory.</summary>
    public const string InstallDirectory = @"C:\Program Files\Servora";

    /// <summary>Default data directory.</summary>
    public const string DataDirectory = @"C:\ProgramData\Servora";

    /// <summary>Backup root.</summary>
    public const string BackupDirectory = @"C:\ProgramData\Servora\backups";

    /// <summary>Initializes a new instance of the <see cref="InstallerTestHost"/> class.</summary>
    public InstallerTestHost()
    {
        Context = new InstallerContext(
            FileSystem,
            Services,
            Processes,
            Probe,
            Environment,
            Ports,
            EventLog,
            Ui,
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 7, 14, 30, 0, TimeSpan.Zero)),
            new InstallerTimings
            {
                ServiceTimeout = TimeSpan.FromSeconds(1),
                HealthTimeout = TimeSpan.FromMilliseconds(150),
                HealthInterval = TimeSpan.FromMilliseconds(5),
                FileRetryCount = 2,
                FileRetryDelay = TimeSpan.Zero,
            });
    }

    /// <summary>Gets the file system.</summary>
    public InMemoryFileSystem FileSystem { get; } = new();

    /// <summary>Gets the service manager.</summary>
    public FakeServiceManager Services { get; } = new();

    /// <summary>Gets the process runner.</summary>
    public FakeProcessRunner Processes { get; } = new();

    /// <summary>Gets the health probe.</summary>
    public FakeHealthProbe Probe { get; } = new();

    /// <summary>Gets the environment.</summary>
    public FakeInstallerEnvironment Environment { get; } = new();

    /// <summary>Gets the port checker.</summary>
    public FakePortChecker Ports { get; } = new();

    /// <summary>Gets the event log registrar.</summary>
    public FakeEventLogRegistrar EventLog { get; } = new();

    /// <summary>Gets the UI.</summary>
    public RecordingUi Ui { get; } = new();

    /// <summary>Gets the installer context.</summary>
    public InstallerContext Context { get; }

    /// <summary>Creates a payload of the given version.</summary>
    /// <param name="version">The version.</param>
    /// <param name="marker">Content marker written into every file.</param>
    /// <returns>The payload.</returns>
    public FakePayloadSource Payload(string version, string marker = "new") =>
        new(FileSystem, version, new Dictionary<string, string>
        {
            ["Servora.dll"] = marker,
            ["appsettings.json"] = """{ "Urls": "http://0.0.0.0:5080" }""",
            [@"wwwroot\css\app.css"] = marker,
        });

    /// <summary>Seeds an installed Servora 1.0.0 with data directory content.</summary>
    /// <param name="state">The service state.</param>
    public void SeedInstallation(ServiceState state = ServiceState.Running)
    {
        Services.Install(Path.Combine(InstallDirectory, "Servora.exe"), state);
        FileSystem.AddFile(Path.Combine(InstallDirectory, "Servora.exe"), "exe 1.0.0", "1.0.0");
        FileSystem.AddFile(Path.Combine(InstallDirectory, "Servora.dll"), "old");
        FileSystem.AddFile(Path.Combine(InstallDirectory, "Obsolete.dll"), "old");
        FileSystem.AddFile(Path.Combine(InstallDirectory, "appsettings.json"), """{ "Urls": "http://0.0.0.0:5080" }""");
        FileSystem.AddFile(Path.Combine(InstallDirectory, @"wwwroot\css\app.css"), "old");

        FileSystem.AddFile(Path.Combine(DataDirectory, "appsettings.json"), """{ "Urls": "http://0.0.0.0:6000", "Logging": { "LogLevel": { "Default": "Debug" } } }""");
        FileSystem.AddFile(Path.Combine(DataDirectory, "database.json"), """{ "Provider": "Sqlite" }""");
        FileSystem.AddFile(Path.Combine(DataDirectory, "servora.db"), "database");
        FileSystem.AddFile(Path.Combine(DataDirectory, @"keys\key-1.xml"), "key");
    }

    /// <summary>Makes the health probe report healthy only while the old (1.0.0) binaries are installed.</summary>
    public void OnlyOldVersionIsHealthy() =>
        Probe.Handler = _ => FileSystem.Content(Path.Combine(InstallDirectory, "Servora.dll")) == "old"
            ? HealthProbeResult.Healthy
            : HealthProbeResult.Unhealthy;
}
