using System.ServiceProcess;
using Servora.Application.Backup;
using Servora.Infrastructure.Backup;

namespace Servora.Web.Backup;

/// <summary>
/// Restarter for the Windows service: reports a non-zero service exit code to the service control manager, so the
/// recovery actions configured by the installer (<c>sc failureflag Servora 1</c>, restart after one minute) start
/// Servora again, which then performs the pending restore.
/// </summary>
/// <param name="lifetime">The application lifetime.</param>
/// <param name="hostLifetime">The host lifetime (the Windows service when running as a service).</param>
/// <param name="logger">The logger.</param>
public sealed class WindowsServiceApplicationRestarter(IHostApplicationLifetime lifetime, IHostLifetime hostLifetime, ILogger<HostApplicationRestarter> logger)
    : HostApplicationRestarter(lifetime, logger)
{
    /// <inheritdoc />
    public override bool RestartsAutomatically => OperatingSystem.IsWindows() && hostLifetime is ServiceBase;

    /// <inheritdoc />
    protected override void OnRestartRequested()
    {
        if (OperatingSystem.IsWindows() && hostLifetime is ServiceBase service)
        {
            service.ExitCode = BackupConstants.RestartExitCode;
        }
    }
}
