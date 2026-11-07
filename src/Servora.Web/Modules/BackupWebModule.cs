using Servora.Application.Backup;
using Servora.Web.Backup;

namespace Servora.Web.Modules;

/// <summary>Host-level registrations of the T-15 Backup &amp; Restore module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class BackupWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddBackupWeb(this WebApplicationBuilder builder)
    {
        // Replaces the infrastructure default so a restore request restarts the Windows service via its recovery actions.
        builder.Services.AddSingleton<IApplicationRestarter, WindowsServiceApplicationRestarter>();
        return builder;
    }
}
