using System.Globalization;
using Servora.Installer.Abstractions;
using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Fresh installation: copy the program files, write the instance settings, register the service (delayed automatic
/// start, restart recovery), secure the directories, optionally open the firewall, start, verify <c>/health</c> and open
/// the setup wizard. Any failure reverts the machine changes (the data directory is left in place).
/// </summary>
/// <param name="context">The installer context.</param>
internal sealed class InstallWorkflow(InstallerContext context)
{
    /// <summary>Runs the installation.</summary>
    /// <param name="options">The options.</param>
    /// <param name="payload">The payload source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<ExitCode> RunAsync(InstallerOptions options, IPayloadSource payload, CancellationToken cancellationToken)
    {
        var ui = context.Ui;
        var fs = context.FileSystem;
        ui.Heading("Install Servora");

        if (context.Services.Query(ProductInfo.ServiceName) is { } existing)
        {
            ui.Error($"The service '{existing.Name}' is already installed. Run 'ServoraSetup update' to update it.");
            return ExitCode.AlreadyInstalled;
        }

        // Directories ------------------------------------------------------------------------------------------
        string dataDirectory;
        if (options.DataDirectory is { } customData)
        {
            if (!Path.IsPathFullyQualified(customData))
            {
                ui.Error("--data-dir must be an absolute path.");
                return ExitCode.InvalidArguments;
            }

            dataDirectory = LayoutResolver.Normalize(customData);
        }
        else
        {
            dataDirectory = context.Layouts.ResolveDataDirectory();
        }

        var installDirectory = options.InstallDirectory ?? context.Layouts.DefaultInstallDirectory;
        while (true)
        {
            if (ui.IsInteractive && options.InstallDirectory is null)
            {
                installDirectory = ui.Ask("Install directory", installDirectory);
            }

            var error = context.Layouts.ValidateInstallDirectory(installDirectory, dataDirectory)
                ?? ValidateInstallDirectoryContent(installDirectory);
            if (error is null)
            {
                break;
            }

            ui.Error(error);
            if (!ui.IsInteractive || options.InstallDirectory is not null)
            {
                return ExitCode.InvalidArguments;
            }
        }

        installDirectory = LayoutResolver.Normalize(installDirectory);
        var layout = new InstallerLayout(installDirectory, dataDirectory, context.Layouts.BackupDirectory);

        // Port -------------------------------------------------------------------------------------------------
        var existingPort = HealthUrlResolver.FirstPort(context.Settings.ReadUrls(layout.InstanceSettingsPath));
        var port = options.Port ?? existingPort ?? ProductInfo.DefaultPort;
        var askPort = ui.IsInteractive && options.Port is null;
        while (true)
        {
            if (askPort)
            {
                var answer = ui.Ask("HTTP port", port.ToString(CultureInfo.InvariantCulture));
                if (!int.TryParse(answer, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)
                {
                    ui.Error("Enter a port number between 1 and 65535.");
                    port = existingPort ?? ProductInfo.DefaultPort;
                    continue;
                }
            }

            if (context.Ports.IsAvailable(port))
            {
                break;
            }

            if (options.Force)
            {
                ui.Warning($"Port {port} is in use; continuing because --force was given.");
                break;
            }

            ui.Error($"Port {port} is already in use by another program.");
            if (!askPort)
            {
                ui.Info("Choose another port with --port <n> or use --force.");
                return ExitCode.Failed;
            }
        }

        var firewall = options.Firewall
            ?? (ui.IsInteractive && ui.Confirm($"Allow inbound connections on TCP port {port} in Windows Firewall (domain and private networks)?", true));

        // Summary ----------------------------------------------------------------------------------------------
        ui.Heading("Summary");
        ui.Detail("Install directory", layout.InstallDirectory, MessageTone.Accent);
        ui.Detail("Data directory", layout.DataDirectory, MessageTone.Accent);
        ui.Detail("Port", port.ToString(CultureInfo.InvariantCulture), MessageTone.Accent);
        ui.Detail("Service account", ScCommandBuilder.AccountName(options.ServiceAccount, ProductInfo.ServiceName));
        ui.Detail("Start mode", "Automatic (delayed start), restart on failure");
        ui.Detail("Firewall rule", firewall ? $"Allow TCP {port} (domain, private)" : "none");
        ui.Detail("Payload", payload.Description, MessageTone.Muted);
        if (ui.IsInteractive && !ui.Confirm("Start the installation?", true))
        {
            ui.Warning("Installation cancelled.");
            return ExitCode.Cancelled;
        }

        // Execution --------------------------------------------------------------------------------------------
        ui.Heading("Installing");
        ui.Step("Extracting application files");
        var staged = context.Stager.Stage(payload);
        ui.Success($"Payload version {ProductVersion.Display(staged.Version)} extracted");

        var installDirectoryExisted = fs.DirectoryExists(layout.InstallDirectory);
        var serviceCreated = false;
        var firewallCreated = false;
        var eventSourceRegistered = false;
        try
        {
            ui.Step($"Copying program files to {layout.InstallDirectory}");
            fs.CreateDirectory(layout.InstallDirectory);
            context.AccessControl.SecureInstallDirectory(layout.InstallDirectory);
            var files = context.Copier.Copy(staged.ApplicationDirectory, layout.InstallDirectory);
            ui.Success($"{files} files copied");

            ui.Step($"Preparing data directory {layout.DataDirectory}");
            fs.CreateDirectory(layout.DataDirectory);
            if (existingPort != port)
            {
                context.Settings.SetUrls(layout.InstanceSettingsPath, InstanceSettingsFile.UrlsForPort(port));
                ui.Success($"Instance settings written ({layout.InstanceSettingsPath})");
            }
            else
            {
                ui.Success("Existing instance settings kept");
            }

            ui.Step("Registering Windows service");
            var environment = options.DataDirectory is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [ProductInfo.DataDirectoryVariable] = layout.DataDirectory };
            context.Services.Create(new ServiceDefinition(
                ProductInfo.ServiceName,
                ProductInfo.ServiceDisplayName,
                ProductInfo.ServiceDescription,
                layout.ExecutablePath,
                options.ServiceAccount,
                environment));
            serviceCreated = true;
            ui.Success($"Service '{ProductInfo.ServiceName}' registered (delayed automatic start, restart on failure)");

            ui.Step("Securing data directory permissions");
            context.AccessControl.SecureDataDirectory(layout.DataDirectory, options.ServiceAccount, ProductInfo.ServiceName);
            ui.Success("Access limited to SYSTEM, Administrators and the service account");

            eventSourceRegistered = TryRegisterEventSource(layout.InstallDirectory);

            if (firewall)
            {
                ui.Step($"Creating firewall rule for TCP port {port}");
                context.Firewall.AddRule(ProductInfo.FirewallRuleName, port);
                firewallCreated = true;
                ui.Success($"Firewall rule '{ProductInfo.FirewallRuleName}' created");
            }

            ui.Step("Starting service");
            context.Services.Start(ProductInfo.ServiceName, context.Timings.ServiceTimeout);
            ui.Success("Service started");

            var healthUrl = context.HealthUrl(layout, options);
            var timeout = context.HealthTimeout(options);
            ui.Step($"Waiting for {healthUrl}");
            var outcome = await context.Health.WaitAsync(
                ProductInfo.ServiceName, healthUrl, timeout, context.Timings.HealthInterval, acceptAnyResponse: true, cancellationToken).ConfigureAwait(false);
            if (outcome != HealthCheckOutcome.Healthy)
            {
                throw new InstallerException(InstallerContext.DescribeHealthFailure(outcome, healthUrl, timeout));
            }

            ui.Success("Servora is responding");
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException)
        {
            ui.Error(ex.Message);
            ui.Warning("Reverting the installation ...");
            Revert(layout, installDirectoryExisted, serviceCreated, firewallCreated, eventSourceRegistered);
            return ex is InstallerException installerException ? installerException.ExitCode : ExitCode.Failed;
        }
        finally
        {
            context.Stager.Cleanup(staged.StagingRoot);
        }

        var baseUrl = HealthUrlResolver.ResolveBase(context.ConfiguredUrls(layout));
        var setupUrl = new Uri(baseUrl, ProductInfo.SetupPath.TrimStart('/'));
        ui.Heading("Done");
        ui.Success($"Servora {ProductVersion.Display(staged.Version)} is installed and running.");
        ui.Detail("Web UI", baseUrl.ToString(), MessageTone.Accent);
        ui.Detail("Setup wizard", setupUrl.ToString(), MessageTone.Accent);
        ui.Info($"Complete the setup wizard from this server (localhost). Remote access to the wizard requires the token in {Path.Combine(layout.DataDirectory, "setup-token.txt")}.");

        if (!options.Silent && !options.NoBrowser && ui.IsInteractive)
        {
            try
            {
                context.Processes.OpenUrl(setupUrl);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                ui.Warning($"Could not open the browser: {ex.Message}");
            }
        }

        return ExitCode.Success;
    }

    private string? ValidateInstallDirectoryContent(string installDirectory)
    {
        var fs = context.FileSystem;
        if (!fs.DirectoryExists(installDirectory))
        {
            return null;
        }

        var empty = fs.GetFiles(installDirectory).Count == 0 && fs.GetDirectories(installDirectory).Count == 0;
        return empty || fs.FileExists(Path.Combine(installDirectory, ProductInfo.ExecutableName))
            ? null
            : $"'{installDirectory}' already contains other files. Choose an empty or new folder.";
    }

    private bool TryRegisterEventSource(string installDirectory)
    {
        try
        {
            context.EventLog.Register(ProductInfo.EventLogSource, installDirectory);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException or InstallerException)
        {
            context.Ui.Warning($"Event log source could not be registered ({ex.Message}). Service events may be missing from the Windows Event Log.");
            return false;
        }
    }

    private void Revert(InstallerLayout layout, bool installDirectoryExisted, bool serviceCreated, bool firewallCreated, bool eventSourceRegistered)
    {
        var ui = context.Ui;
        if (serviceCreated)
        {
            TryRun(() => context.Services.Stop(ProductInfo.ServiceName, context.Timings.ServiceTimeout), "stop the service");
            TryRun(() => context.Services.Delete(ProductInfo.ServiceName), "remove the service");
        }

        if (firewallCreated)
        {
            TryRun(() => context.Firewall.RemoveRule(ProductInfo.FirewallRuleName), "remove the firewall rule");
        }

        if (eventSourceRegistered)
        {
            TryRun(() => context.EventLog.Unregister(ProductInfo.EventLogSource), "remove the event log source");
        }

        TryRun(
            () =>
            {
                if (installDirectoryExisted)
                {
                    context.Copier.Clear(layout.InstallDirectory, LayoutResolver.PreservedEntries(layout.InstallDirectory, layout.DataDirectory));
                }
                else
                {
                    context.Copier.Delete(layout.InstallDirectory);
                }
            },
            "remove the program files");

        ui.Info($"Installation reverted. The data directory {layout.DataDirectory} was left in place.");
    }

    private void TryRun(Action action, string description)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is InstallerException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            context.Ui.Warning($"Could not {description}: {ex.Message}");
        }
    }
}
