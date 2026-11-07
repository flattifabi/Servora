using Servora.Installer.Abstractions;
using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>Dependencies and shared helpers of the installer workflows (composition root output).</summary>
internal sealed class InstallerContext
{
    /// <summary>Initializes a new instance of the <see cref="InstallerContext"/> class.</summary>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="services">The service manager.</param>
    /// <param name="processes">The process runner.</param>
    /// <param name="healthProbe">The health probe.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="ports">The port checker.</param>
    /// <param name="eventLog">The event log registrar.</param>
    /// <param name="ui">The operator UI.</param>
    /// <param name="time">The clock.</param>
    /// <param name="timings">Timeouts and retry settings.</param>
    public InstallerContext(
        IFileSystem fileSystem,
        IServiceManager services,
        IProcessRunner processes,
        IHealthProbe healthProbe,
        IInstallerEnvironment environment,
        IPortChecker ports,
        IEventLogRegistrar eventLog,
        IInstallerUi ui,
        TimeProvider time,
        InstallerTimings timings)
    {
        FileSystem = fileSystem;
        Services = services;
        Processes = processes;
        Environment = environment;
        Ports = ports;
        EventLog = eventLog;
        Ui = ui;
        Time = time;
        Timings = timings;
        Copier = new FileTreeCopier(fileSystem, timings);
        Backups = new BackupManager(fileSystem, Copier, time);
        Settings = new InstanceSettingsFile(fileSystem);
        Layouts = new LayoutResolver(services, environment);
        Firewall = new FirewallManager(processes, environment);
        AccessControl = new AccessControlManager(processes, environment);
        Stager = new PayloadStager(fileSystem, environment);
        Health = new HealthMonitor(healthProbe, services, time);
        HealthProbe = healthProbe;
    }

    /// <summary>Gets the file system.</summary>
    public IFileSystem FileSystem { get; }

    /// <summary>Gets the service manager.</summary>
    public IServiceManager Services { get; }

    /// <summary>Gets the process runner.</summary>
    public IProcessRunner Processes { get; }

    /// <summary>Gets the health probe.</summary>
    public IHealthProbe HealthProbe { get; }

    /// <summary>Gets the environment.</summary>
    public IInstallerEnvironment Environment { get; }

    /// <summary>Gets the port checker.</summary>
    public IPortChecker Ports { get; }

    /// <summary>Gets the event log registrar.</summary>
    public IEventLogRegistrar EventLog { get; }

    /// <summary>Gets the operator UI.</summary>
    public IInstallerUi Ui { get; }

    /// <summary>Gets the clock.</summary>
    public TimeProvider Time { get; }

    /// <summary>Gets the timings.</summary>
    public InstallerTimings Timings { get; }

    /// <summary>Gets the tree copier.</summary>
    public FileTreeCopier Copier { get; }

    /// <summary>Gets the backup manager.</summary>
    public BackupManager Backups { get; }

    /// <summary>Gets the instance settings accessor.</summary>
    public InstanceSettingsFile Settings { get; }

    /// <summary>Gets the layout resolver.</summary>
    public LayoutResolver Layouts { get; }

    /// <summary>Gets the firewall manager.</summary>
    public FirewallManager Firewall { get; }

    /// <summary>Gets the ACL manager.</summary>
    public AccessControlManager AccessControl { get; }

    /// <summary>Gets the payload stager.</summary>
    public PayloadStager Stager { get; }

    /// <summary>Gets the health monitor.</summary>
    public HealthMonitor Health { get; }

    /// <summary>Gets the configured listen URLs of an installation (instance settings first, then shipped defaults).</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>The URLs.</returns>
    public IReadOnlyList<string> ConfiguredUrls(InstallerLayout layout) =>
        [
            .. Settings.ReadUrls(layout.InstanceSettingsPath),
            .. Settings.ReadUrls(Path.Combine(layout.InstallDirectory, ProductInfo.InstanceSettingsFileName)),
        ];

    /// <summary>Resolves the health URL for an installation.</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="options">The options (explicit <c>--health-url</c> wins).</param>
    /// <returns>The health URL.</returns>
    public Uri HealthUrl(InstallerLayout layout, InstallerOptions options) =>
        options.HealthUrl ?? HealthUrlResolver.Resolve(ConfiguredUrls(layout));

    /// <summary>Gets the health timeout (option override or default).</summary>
    /// <param name="options">The options.</param>
    /// <returns>The timeout.</returns>
    public TimeSpan HealthTimeout(InstallerOptions options) => options.HealthTimeout ?? Timings.HealthTimeout;

    /// <summary>Describes a failed health check for the operator.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="url">The probed URL.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>The message.</returns>
    public static string DescribeHealthFailure(HealthCheckOutcome outcome, Uri url, TimeSpan timeout) => outcome switch
    {
        HealthCheckOutcome.ServiceStopped => "The service stopped unexpectedly after start. Check the Windows Event Log (Application, source Servora) and the logs in the data directory.",
        _ => $"{url} did not report healthy within {timeout.TotalSeconds:0} seconds.",
    };
}
