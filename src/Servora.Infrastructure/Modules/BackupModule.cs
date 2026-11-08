using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Servora.Application.Backup;
using Servora.Infrastructure.Backup;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Backup &amp; Restore (T-15) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class BackupModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBackupModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IBackupDatabase, ServoraBackupDatabase>();
        services.AddSingleton<BackupSettingsStore>();
        services.AddSingleton<BackupCatalog>();
        services.AddSingleton<BackupOperationLock>();
        services.AddSingleton<BackupEngine>();
        services.AddSingleton<BackupRetentionService>();
        services.AddSingleton<IBackupPasswordSource, SecretStoreBackupPasswordSource>();
        services.AddSingleton<BackupJobRunner>();
        services.AddSingleton<RestoreRequestStore>();
        services.AddSingleton<PendingRestoreProcessor>();
        services.AddSingleton<DatabaseMigrationRunner>();
        services.AddSingleton<BackupAuditFlusher>();
        services.AddSingleton<IApplicationRestarter, HostApplicationRestarter>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<DataResetEngine>();
        services.AddScoped<IDataResetService, DataResetService>();
        services.AddHostedService<BackupSchedulerService>();
        return services;
    }
}
