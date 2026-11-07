using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Read-only status report. Works without administrative rights; sections that need rights the caller does not have
/// are reported as unavailable instead of failing.
/// </summary>
/// <param name="context">The installer context.</param>
internal sealed class StatusReporter(InstallerContext context)
{
    /// <summary>Prints the status.</summary>
    /// <param name="options">The options.</param>
    /// <param name="installerVersion">The installer version.</param>
    /// <param name="payloadDescription">Description of the available payload, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code (always <see cref="ExitCode.Success"/>; the report itself shows problems).</returns>
    public async Task<ExitCode> RunAsync(InstallerOptions options, string installerVersion, string? payloadDescription, CancellationToken cancellationToken)
    {
        var ui = context.Ui;
        ui.Heading("Servora status");

        var service = context.Services.Query(ProductInfo.ServiceName);
        if (service is null)
        {
            ui.Detail("Service", "not installed", MessageTone.Warning);
            ui.Detail("Installer", ProductVersion.Display(installerVersion));
            ui.Detail("Payload", payloadDescription ?? "none found", payloadDescription is null ? MessageTone.Warning : MessageTone.Normal);
            ui.Detail("Elevated", context.Environment.IsElevated ? "yes" : "no", MessageTone.Muted);
            ui.Info("Run 'ServoraSetup install' (as administrator) to install Servora.");
            return ExitCode.Success;
        }

        var layout = context.Layouts.ForInstalledService(service);
        var installed = Safe(() => context.FileSystem.GetProductVersion(layout.ExecutablePath));

        ui.Detail("Service", $"{service.Name} ({service.DisplayName})");
        ui.Detail("State", service.State.ToString(), service.State switch
        {
            ServiceState.Running => MessageTone.Good,
            ServiceState.Stopped => MessageTone.Bad,
            _ => MessageTone.Warning,
        });
        ui.Detail("Start mode", service.StartMode + (service.DelayedAutoStart ? " (delayed start)" : string.Empty));
        ui.Detail("Account", service.Account);
        ui.Detail("Install directory", layout.InstallDirectory);
        ui.Detail("Installed version", ProductVersion.Display(installed), MessageTone.Accent);
        ui.Detail("Data directory", layout.DataDirectory);

        var urls = Safe(() => context.ConfiguredUrls(layout)) ?? [];
        var healthUrl = options.HealthUrl ?? HealthUrlResolver.Resolve(urls);
        ui.Detail("Listen URLs", urls.Count > 0 ? string.Join("; ", urls) : "default (http://0.0.0.0:5080)");

        var health = service.State == ServiceState.Running
            ? await context.HealthProbe.ProbeAsync(healthUrl, cancellationToken).ConfigureAwait(false)
            : HealthProbeResult.Unreachable;
        ui.Detail("Health", $"{health} ({healthUrl})", health switch
        {
            HealthProbeResult.Healthy => MessageTone.Good,
            HealthProbeResult.Unhealthy => MessageTone.Warning,
            _ => MessageTone.Bad,
        });

        var backups = Safe(() => context.Backups.List(layout.BackupDirectory));
        ui.Detail("Backups", backups is null
            ? "unavailable (run elevated to list)"
            : backups.Count == 0 ? "none" : $"{backups.Count} (latest {Path.GetFileName(backups[0])})");

        ui.Detail("Installer", ProductVersion.Display(installerVersion));
        ui.Detail("Payload", payloadDescription ?? "none found", MessageTone.Muted);
        ui.Detail("Elevated", context.Environment.IsElevated ? "yes" : "no", MessageTone.Muted);
        return ExitCode.Success;
    }

    private static T? Safe<T>(Func<T?> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InstallerException)
        {
            return null;
        }
    }
}
