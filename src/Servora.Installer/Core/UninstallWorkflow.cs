using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Removes the service, firewall rule, event log source and program files. The data directory (database settings,
/// SQLite database, Data Protection keys, attachments, logs, backups) is deleted unless <c>--keep-data</c> is given;
/// interactive runs ask and default to keeping it. External SQL Server / PostgreSQL databases are never touched.
/// </summary>
/// <param name="context">The installer context.</param>
internal sealed class UninstallWorkflow(InstallerContext context)
{
    /// <summary>Runs the removal.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The exit code.</returns>
    public ExitCode Run(InstallerOptions options)
    {
        var ui = context.Ui;
        ui.Heading("Uninstall Servora");

        var service = context.Services.Query(ProductInfo.ServiceName);
        if (service is null)
        {
            ui.Error($"The service '{ProductInfo.ServiceName}' is not installed.");
            return ExitCode.NotInstalled;
        }

        // Resolve the layout before the service (and its environment) is deleted.
        var layout = context.Layouts.ForInstalledService(service);
        ui.Detail("Install directory", layout.InstallDirectory);
        ui.Detail("Data directory", layout.DataDirectory);

        var keepData = options.KeepData;
        if (ui.IsInteractive)
        {
            if (!ui.Confirm("Remove the Servora service and program files?", false))
            {
                ui.Warning("Uninstall cancelled.");
                return ExitCode.Cancelled;
            }

            if (!keepData)
            {
                keepData = !ui.Confirm(
                    $"Also delete the data directory {layout.DataDirectory} (database settings, SQLite database, encryption keys, attachments, logs, backups)? This cannot be undone",
                    false);
            }
        }

        ui.Heading("Removing");
        var warnings = 0;
        ui.Step("Stopping service");
        try
        {
            context.Services.Stop(ProductInfo.ServiceName, context.Timings.ServiceTimeout);
            ui.Success("Service stopped");
        }
        catch (InstallerException ex)
        {
            ui.Error(ex.Message);
            ui.Info("Nothing was removed. Stop the service manually (services.msc) and retry.");
            return ExitCode.Failed;
        }

        ui.Step("Removing Windows service");
        context.Services.Delete(ProductInfo.ServiceName);
        ui.Success("Service removed");

        if (context.Firewall.RemoveRule(ProductInfo.FirewallRuleName))
        {
            ui.Success($"Firewall rule '{ProductInfo.FirewallRuleName}' removed");
        }

        try
        {
            context.EventLog.Unregister(ProductInfo.EventLogSource);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            ui.Warning($"Event log source not removed: {ex.Message}");
            warnings++;
        }

        ui.Step($"Deleting program files {layout.InstallDirectory}");
        if (LayoutResolver.IsSameOrInside(LayoutResolver.Normalize(context.Environment.InstallerDirectory), layout.InstallDirectory))
        {
            ui.Warning("ServoraSetup.exe runs from the install directory, which therefore cannot be deleted. Delete it manually.");
            warnings++;
        }
        else
        {
            warnings += TryDelete(layout.InstallDirectory);
        }

        if (keepData)
        {
            ui.Info($"Data directory kept: {layout.DataDirectory}");
        }
        else
        {
            ui.Step($"Deleting data directory {layout.DataDirectory}");
            warnings += TryDelete(layout.DataDirectory);
            var defaultData = context.Layouts.DefaultDataDirectory;
            if (!string.Equals(defaultData, layout.DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // Backups and installer logs always live below %ProgramData%\Servora.
                warnings += TryDelete(defaultData);
            }

            ui.Info("External SQL Server or PostgreSQL databases were not modified.");
        }

        ui.Heading("Done");
        if (warnings == 0)
        {
            ui.Success("Servora was removed.");
        }
        else
        {
            ui.Warning($"Servora was removed with {warnings} warning(s); see above.");
        }

        return ExitCode.Success;
    }

    private int TryDelete(string directory)
    {
        try
        {
            context.Copier.Delete(directory);
            context.Ui.Success($"Deleted {directory}");
            return 0;
        }
        catch (InstallerException ex)
        {
            context.Ui.Warning(ex.Message);
            return 1;
        }
    }
}
