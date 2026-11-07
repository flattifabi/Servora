using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servora.Application.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Default <see cref="IApplicationRestarter"/>: sets a non-zero process exit code and stops the host after a short delay
/// (so the HTTP response / Blazor update still reaches the browser). The web host replaces it with a Windows-service
/// aware implementation that also reports the exit code to the service control manager.
/// </summary>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="logger">The logger.</param>
public class HostApplicationRestarter(IHostApplicationLifetime lifetime, ILogger<HostApplicationRestarter> logger) : IApplicationRestarter
{
    /// <inheritdoc />
    public virtual bool RestartsAutomatically => false;

    /// <inheritdoc />
    public void RequestRestart(string reason)
    {
        logger.LogWarning("Servora stops to restart: {Reason}. Exit code {ExitCode}.", reason, BackupConstants.RestartExitCode);
        Environment.ExitCode = BackupConstants.RestartExitCode;
        OnRestartRequested();
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            lifetime.StopApplication();
        });
    }

    /// <summary>Called before the application is stopped (sets host-specific exit codes).</summary>
    protected virtual void OnRestartRequested()
    {
    }
}
