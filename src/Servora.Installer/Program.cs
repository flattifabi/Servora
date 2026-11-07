using System.Reflection;
using Servora.Installer.Cli;
using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Installer.Payload;
using Servora.Installer.Platform;

// ---------------------------------------------------------------------------------------------------------------
// ServoraSetup.exe – installs, updates and removes the Servora Windows service. See docs/operations/installation.md.
// ---------------------------------------------------------------------------------------------------------------
var parse = CommandLineParser.Parse(args);
var options = parse.Options;
var ui = new ConsoleUi(options.Silent);
var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "0.0.0";

if (!options.Silent)
{
    ui.Banner(ProductVersion.Display(version));
}

if (!parse.IsValid)
{
    foreach (var error in parse.Errors)
    {
        ui.Error(error);
    }

    ui.Info("Run 'ServoraSetup --help' for usage.");
    return (int)ExitCode.InvalidArguments;
}

if (!OperatingSystem.IsWindows())
{
    ui.Error("ServoraSetup runs on Windows only.");
    return (int)ExitCode.Failed;
}

var environment = new WindowsInstallerEnvironment();

// Commands that change the machine need administrator rights. Interactive runs relaunch through UAC; silent runs fail
// fast so that automation never blocks on a prompt.
if (CommandLineParser.RequiresElevation(options.Command) && !environment.IsElevated && !options.Elevated)
{
    if (options.Silent || !ui.IsInteractive)
    {
        ui.Error("Administrator rights are required. Run the command from an elevated prompt (Run as administrator).");
        return (int)ExitCode.ElevationRequired;
    }

    ui.Info("Administrator rights are required - requesting elevation ...");
    var elevatedExitCode = ElevatedRelauncher.Relaunch(args);
    if (elevatedExitCode is null)
    {
        ui.Warning("Elevation was declined.");
        return (int)ExitCode.Cancelled;
    }

    return elevatedExitCode.Value;
}

var processes = new SystemProcessRunner(environment);
using var healthProbe = new HttpHealthProbe();
var context = new InstallerContext(
    new PhysicalFileSystem(),
    new WindowsServiceManager(processes, environment),
    processes,
    healthProbe,
    environment,
    new TcpPortChecker(),
    new WindowsEventLogRegistrar(environment),
    ui,
    TimeProvider.System,
    InstallerTimings.Default);
var locator = new PayloadLocator(environment, Assembly.GetExecutingAssembly());
var app = new InstallerApp(context, locator.Locate, version);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // The first Ctrl+C requests cancellation; workflows still finish or roll back the current step.
    e.Cancel = true;
    cancellation.Cancel();
};

var exitCode = await app.RunAsync(options, cancellation.Token);

if (options.Elevated && ui.IsInteractive)
{
    // The elevated window closes on exit; keep the result visible.
    ui.Pause("Press Enter to close this window ...");
}

return (int)exitCode;
