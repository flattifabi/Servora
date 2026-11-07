using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Observability;

/// <summary>
/// Background maintenance of the Observability module: restores the persisted debug switch at start-up, reverts
/// time-limited debug sessions, samples process metrics, deletes expired log files and prunes the audit log according to
/// its retention (bounded batches). Publishes its own heartbeat.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="databaseSettings">The database settings (database tasks are skipped in setup mode).</param>
/// <param name="processMetrics">The process metrics.</param>
/// <param name="heartbeats">The heartbeat registry.</param>
/// <param name="logger">The logger.</param>
public sealed class ObservabilityMaintenanceService(
    IServiceScopeFactory scopeFactory,
    DatabaseSettingsStore databaseSettings,
    IProcessMetrics processMetrics,
    IHeartbeatRegistry heartbeats,
    ILogger<ObservabilityMaintenanceService> logger) : BackgroundService
{
    /// <summary>The heartbeat name of this service.</summary>
    public const string HeartbeatName = "Observability maintenance";

    private static readonly TimeSpan s_tick = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan s_retentionInterval = TimeSpan.FromHours(6);
    private const int MaxPruneBatchesPerRun = 20;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var restored = false;
        var nextRetention = DateTimeOffset.UtcNow.AddMinutes(1);
        var nextSample = DateTimeOffset.MinValue;

        using var timer = new PeriodicTimer(s_tick);
        do
        {
            try
            {
                if (DateTimeOffset.UtcNow >= nextSample)
                {
                    processMetrics.Sample();
                    nextSample = DateTimeOffset.UtcNow.AddSeconds(30);
                }

                if (databaseSettings.IsConfigured)
                {
                    if (!restored)
                    {
                        await RunAsSystemAsync<ILoggingAdminService>(s => s.RestorePersistedAsync(stoppingToken));
                        restored = true;
                    }

                    await RunAsSystemAsync<ILoggingAdminService>(s => s.RevertIfDueAsync(stoppingToken));

                    if (DateTimeOffset.UtcNow >= nextRetention)
                    {
                        await RunRetentionAsync(stoppingToken);
                        nextRetention = DateTimeOffset.UtcNow + s_retentionInterval;
                    }
                }

                heartbeats.Beat(HeartbeatName, s_tick, databaseSettings.IsConfigured ? "ok" : "waiting for setup");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Observability maintenance run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunRetentionAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Observability retention"));

        var files = scope.ServiceProvider.GetService<ILogFileStore>();
        var deletedFiles = files?.ApplyRetention() ?? 0;
        if (deletedFiles > 0)
        {
            logger.LogInformation("Log retention deleted {Count} expired log file(s).", deletedFiles);
        }

        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        for (var batch = 0; batch < MaxPruneBatchesPerRun; batch++)
        {
            if (await auditLog.PruneAsync(cancellationToken) == 0)
            {
                break;
            }
        }
    }

    private async Task RunAsSystemAsync<TService>(Func<TService, Task> action)
        where TService : notnull
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Observability"));
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
