using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>Interactive main menu shown when <c>ServoraSetup.exe</c> is started without a command.</summary>
/// <param name="app">The application dispatcher.</param>
internal sealed class InteractiveMenu(InstallerApp app)
{
    /// <summary>Shows the menu until the operator exits.</summary>
    /// <param name="options">The options (applied to every action).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The exit code of the last action.</returns>
    public async Task<ExitCode> RunAsync(InstallerOptions options, CancellationToken cancellationToken)
    {
        var context = app.Context;
        var ui = context.Ui;
        var last = ExitCode.Success;

        while (true)
        {
            var service = context.Services.Query(ProductInfo.ServiceName);
            var payload = app.DescribePayload(options);
            var actions = new List<(string Label, Func<Task<ExitCode>> Run)>();

            if (service is null)
            {
                ui.Detail("Servora", "not installed", MessageTone.Warning);
                actions.Add(("Install Servora", () => app.InstallAsync(options with { Command = InstallerCommand.Install }, cancellationToken)));
            }
            else
            {
                var layout = context.Layouts.ForInstalledService(service);
                var installed = ProductVersion.Display(context.FileSystem.GetProductVersion(layout.ExecutablePath));
                ui.Detail("Servora", $"{installed} installed, service {service.State}", service.State == ServiceState.Running ? MessageTone.Good : MessageTone.Warning);
                actions.Add(($"Update to {ProductVersion.Display(app.InstallerVersion)} (backup + automatic rollback)", () => app.UpdateAsync(options with { Command = InstallerCommand.Update }, cancellationToken)));
                actions.Add(("Open the web UI", () =>
                {
                    context.Processes.OpenUrl(HealthUrlResolver.ResolveBase(context.ConfiguredUrls(layout)));
                    return Task.FromResult(ExitCode.Success);
                }));
                actions.Add(("Uninstall", () => Task.FromResult(app.Uninstall(options with { Command = InstallerCommand.Uninstall }))));
            }

            actions.Add(("Show detailed status", () => app.StatusAsync(options, cancellationToken)));
            ui.Detail("Payload", payload ?? "none found", MessageTone.Muted);

            var choice = ui.Choose("What would you like to do?", actions.Select(a => a.Label).ToList());
            if (choice < 0 || choice >= actions.Count)
            {
                return last;
            }

            try
            {
                last = await actions[choice].Run().ConfigureAwait(false);
            }
            catch (InstallerException ex)
            {
                ui.Error(ex.Message);
                last = ex.ExitCode;
            }

            ui.Pause("Press Enter to return to the menu ...");
        }
    }
}
