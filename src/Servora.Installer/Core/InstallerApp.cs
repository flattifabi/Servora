using System.Globalization;
using Servora.Installer.Abstractions;
using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>Dispatches a parsed command line to the workflows.</summary>
/// <param name="context">The installer context.</param>
/// <param name="payloadResolver">Locates the payload for the options (returns <see langword="null"/> when none exists).</param>
/// <param name="installerVersion">The installer version.</param>
internal sealed class InstallerApp(InstallerContext context, Func<InstallerOptions, IPayloadSource?> payloadResolver, string installerVersion)
{
    /// <summary>Gets the installer version.</summary>
    public string InstallerVersion => installerVersion;

    /// <summary>Gets the installer context.</summary>
    public InstallerContext Context => context;

    /// <summary>Runs a command.</summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<ExitCode> RunAsync(InstallerOptions options, CancellationToken cancellationToken)
    {
        var ui = context.Ui;
        try
        {
            switch (options.Command)
            {
                case InstallerCommand.Help:
                    ui.Info(HelpText.Usage);
                    return ExitCode.Success;
                case InstallerCommand.Version:
                    ui.Detail("Installer", ProductVersion.Display(installerVersion));
                    ui.Detail("Payload", DescribePayload(options) ?? "none found");
                    return ExitCode.Success;
                case InstallerCommand.Status:
                    return await StatusAsync(options, cancellationToken).ConfigureAwait(false);
            }

            if (!context.Environment.IsElevated)
            {
                ui.Error("This command requires administrator rights. Run ServoraSetup.exe from an elevated prompt (Run as administrator).");
                return ExitCode.ElevationRequired;
            }

            StartTranscript();
            return options.Command switch
            {
                InstallerCommand.Install => await InstallAsync(options, cancellationToken).ConfigureAwait(false),
                InstallerCommand.Update => await UpdateAsync(options, cancellationToken).ConfigureAwait(false),
                InstallerCommand.Uninstall => Uninstall(options),
                _ when ui.IsInteractive => await new InteractiveMenu(this).RunAsync(options, cancellationToken).ConfigureAwait(false),
                _ => context.Services.Query(ProductInfo.ServiceName) is null
                    ? await InstallAsync(options, cancellationToken).ConfigureAwait(false)
                    : await UpdateAsync(options, cancellationToken).ConfigureAwait(false),
            };
        }
        catch (InstallerException ex)
        {
            ui.Error(ex.Message);
            return ex.ExitCode;
        }
        catch (OperationCanceledException)
        {
            ui.Warning("Cancelled.");
            return ExitCode.Cancelled;
        }
    }

    /// <summary>Runs the installation workflow.</summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<ExitCode> InstallAsync(InstallerOptions options, CancellationToken cancellationToken)
    {
        var payload = RequirePayload(options);
        return payload is null
            ? ExitCode.PayloadMissing
            : await new InstallWorkflow(context).RunAsync(options, payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs the update workflow.</summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<ExitCode> UpdateAsync(InstallerOptions options, CancellationToken cancellationToken)
    {
        var payload = RequirePayload(options);
        return payload is null
            ? ExitCode.PayloadMissing
            : await new UpdateWorkflow(context).RunAsync(options, payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs the uninstall workflow.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The exit code.</returns>
    public ExitCode Uninstall(InstallerOptions options) => new UninstallWorkflow(context).Run(options);

    /// <summary>Prints the status report.</summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code.</returns>
    public Task<ExitCode> StatusAsync(InstallerOptions options, CancellationToken cancellationToken) =>
        new StatusReporter(context).RunAsync(options, installerVersion, DescribePayload(options), cancellationToken);

    /// <summary>Describes the payload that would be used, without extracting it.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The description, or <see langword="null"/> when no payload exists.</returns>
    public string? DescribePayload(InstallerOptions options)
    {
        try
        {
            return payloadResolver(options)?.Description;
        }
        catch (InstallerException ex)
        {
            return ex.Message;
        }
    }

    private IPayloadSource? RequirePayload(InstallerOptions options)
    {
        var payload = payloadResolver(options);
        if (payload is null)
        {
            context.Ui.Error(
                "No application payload found. Use the ServoraSetup-<version>-win-x64.exe from GitHub Releases, place the published application in an 'app' folder next to ServoraSetup.exe, or pass --source <folder or zip>.");
        }

        return payload;
    }

    private void StartTranscript()
    {
        var stamp = context.Time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        context.Ui.StartTranscript(Path.Combine(context.Layouts.DefaultDataDirectory, "logs", $"setup-{stamp}.log"));
    }
}
