using Servora.Installer.Abstractions;
using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// In-place update: stage and validate the new payload, stop the service, back up the program files to
/// <c>%ProgramData%\Servora\backups\&lt;timestamp&gt;</c>, replace the program files, start the service and verify
/// <c>/health</c>. Any failure after the backup restores the previous version automatically. The data directory and
/// the instance <c>appsettings.json</c> are never touched.
/// </summary>
/// <param name="context">The installer context.</param>
internal sealed class UpdateWorkflow(InstallerContext context)
{
    /// <summary>Runs the update.</summary>
    /// <param name="options">The options.</param>
    /// <param name="payload">The payload source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<ExitCode> RunAsync(InstallerOptions options, IPayloadSource payload, CancellationToken cancellationToken)
    {
        var ui = context.Ui;
        var fs = context.FileSystem;
        ui.Heading("Update Servora");

        var service = context.Services.Query(ProductInfo.ServiceName);
        if (service is null)
        {
            ui.Error($"The service '{ProductInfo.ServiceName}' is not installed. Run 'ServoraSetup install' first.");
            return ExitCode.NotInstalled;
        }

        var layout = context.Layouts.ForInstalledService(service);
        if (!fs.FileExists(layout.ExecutablePath))
        {
            ui.Error($"The registered service executable '{layout.ExecutablePath}' does not exist. Repair the installation with 'uninstall --keep-data' followed by 'install'.");
            return ExitCode.Failed;
        }

        if (options.InstallDirectory is { } requested
            && !string.Equals(LayoutResolver.Normalize(requested), layout.InstallDirectory, StringComparison.OrdinalIgnoreCase))
        {
            ui.Warning($"--install-dir is ignored for updates; the existing installation in {layout.InstallDirectory} is updated.");
        }

        var installedVersion = fs.GetProductVersion(layout.ExecutablePath);
        var staged = context.Stager.Stage(payload);
        try
        {
            ui.Detail("Install directory", layout.InstallDirectory, MessageTone.Accent);
            ui.Detail("Data directory", layout.DataDirectory + " (not modified)", MessageTone.Accent);
            ui.Detail("Installed version", ProductVersion.Display(installedVersion));
            ui.Detail("New version", ProductVersion.Display(staged.Version), MessageTone.Good);

            var comparison = ProductVersion.Compare(staged.Version, installedVersion);
            if (comparison < 0 && !options.Force)
            {
                ui.Error("The payload is older than the installed version. Downgrades can break the database schema; use --force only together with a database restore.");
                return ExitCode.Failed;
            }

            if (comparison == 0)
            {
                ui.Warning("The installed version is already current; the program files will be refreshed (repair).");
            }

            if (ui.IsInteractive && !ui.Confirm("Stop the service and update now?", true))
            {
                ui.Warning("Update cancelled.");
                return ExitCode.Cancelled;
            }

            return await ApplyAsync(options, layout, service, installedVersion, staged, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            context.Stager.Cleanup(staged.StagingRoot);
        }
    }

    private async Task<ExitCode> ApplyAsync(
        InstallerOptions options,
        InstallerLayout layout,
        ServiceInfo service,
        string? installedVersion,
        StagedPayload staged,
        CancellationToken cancellationToken)
    {
        var ui = context.Ui;
        var preserved = LayoutResolver.PreservedEntries(layout.InstallDirectory, layout.DataDirectory);

        ui.Heading("Updating");
        if (service.State != ServiceState.Stopped)
        {
            ui.Step("Stopping service");
            try
            {
                context.Services.Stop(ProductInfo.ServiceName, context.Timings.ServiceTimeout);
            }
            catch (InstallerException ex)
            {
                ui.Error(ex.Message);
                ui.Info("No files were changed.");
                return ExitCode.Failed;
            }

            ui.Success("Service stopped");
        }

        ui.Step($"Backing up program files to {layout.BackupDirectory}");
        string backupPath;
        try
        {
            backupPath = context.Backups.Create(layout.InstallDirectory, layout.BackupDirectory, installedVersion, preserved);
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException)
        {
            ui.Error($"Backup failed: {ex.Message}");
            ui.Info("No files were changed; restarting the previous version.");
            TryStart();
            return ExitCode.Failed;
        }

        ui.Success($"Backup created: {backupPath}");

        try
        {
            ui.Step("Replacing program files");
            context.Copier.Clear(layout.InstallDirectory, preserved);
            var files = context.Copier.Copy(staged.ApplicationDirectory, layout.InstallDirectory);
            ui.Success($"{files} files copied");

            RefreshEventSource(layout.InstallDirectory);

            ui.Step("Starting service");
            context.Services.Start(ProductInfo.ServiceName, context.Timings.ServiceTimeout);
            ui.Success("Service started");

            var healthUrl = context.HealthUrl(layout, options);
            var timeout = context.HealthTimeout(options);
            ui.Step($"Waiting for {healthUrl}");
            var outcome = await context.Health.WaitAsync(
                ProductInfo.ServiceName, healthUrl, timeout, context.Timings.HealthInterval, acceptAnyResponse: false, cancellationToken).ConfigureAwait(false);
            if (outcome != HealthCheckOutcome.Healthy)
            {
                throw new InstallerException(InstallerContext.DescribeHealthFailure(outcome, healthUrl, timeout));
            }

            ui.Success("Servora is healthy");
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException)
        {
            ui.Error($"Update failed: {ex.Message}");
            return await RollbackAsync(options, layout, backupPath, installedVersion, preserved, cancellationToken).ConfigureAwait(false);
        }

        var pruned = context.Backups.Prune(layout.BackupDirectory, options.KeepBackups, backupPath);
        if (pruned.Count > 0)
        {
            ui.Info($"Removed {pruned.Count} old backup(s); keeping the latest {options.KeepBackups}.");
        }

        ui.Heading("Done");
        ui.Success($"Servora was updated from {ProductVersion.Display(installedVersion)} to {ProductVersion.Display(staged.Version)}.");
        ui.Detail("Web UI", HealthUrlResolver.ResolveBase(context.ConfiguredUrls(layout)).ToString(), MessageTone.Accent);
        ui.Detail("Backup", backupPath, MessageTone.Muted);
        return ExitCode.Success;
    }

    private async Task<ExitCode> RollbackAsync(
        InstallerOptions options,
        InstallerLayout layout,
        string backupPath,
        string? installedVersion,
        IReadOnlySet<string> preserved,
        CancellationToken cancellationToken)
    {
        var ui = context.Ui;
        ui.Heading("Rolling back");
        try
        {
            ui.Step("Stopping service");
            context.Services.Stop(ProductInfo.ServiceName, context.Timings.ServiceTimeout);

            ui.Step($"Restoring program files from {backupPath}");
            context.Backups.Restore(backupPath, layout.InstallDirectory, preserved);
            RefreshEventSource(layout.InstallDirectory);

            ui.Step("Starting previous version");
            context.Services.Start(ProductInfo.ServiceName, context.Timings.ServiceTimeout);

            var healthUrl = context.HealthUrl(layout, options);
            var timeout = context.HealthTimeout(options);
            var outcome = await context.Health.WaitAsync(
                ProductInfo.ServiceName, healthUrl, timeout, context.Timings.HealthInterval, acceptAnyResponse: true, cancellationToken).ConfigureAwait(false);
            if (outcome == HealthCheckOutcome.Healthy)
            {
                ui.Success($"Rolled back to version {ProductVersion.Display(installedVersion)}; the service is running.");
            }
            else
            {
                ui.Warning($"Version {ProductVersion.Display(installedVersion)} was restored, but {InstallerContext.DescribeHealthFailure(outcome, healthUrl, timeout)}");
            }

            ui.Info("The failed update left no changes behind. See docs/operations/troubleshooting.md.");
            return ExitCode.RolledBack;
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException)
        {
            ui.Error($"Rollback failed: {ex.Message}");
            ui.Error($"Restore manually: stop the service, copy the content of '{backupPath}' into '{layout.InstallDirectory}', start the service (docs/operations/upgrade.md).");
            return ExitCode.Failed;
        }
    }

    private void RefreshEventSource(string installDirectory)
    {
        try
        {
            context.EventLog.Register(ProductInfo.EventLogSource, installDirectory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException or InstallerException)
        {
            context.Ui.Warning($"Event log source could not be refreshed: {ex.Message}");
        }
    }

    private void TryStart()
    {
        try
        {
            context.Services.Start(ProductInfo.ServiceName, context.Timings.ServiceTimeout);
        }
        catch (InstallerException ex)
        {
            context.Ui.Warning(ex.Message);
        }
    }
}
