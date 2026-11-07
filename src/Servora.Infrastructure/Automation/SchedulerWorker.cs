using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Scheduling;
using Servora.Application.Common;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Automation;

/// <summary>
/// Background scheduler: every <see cref="FlowEngineOptions.SchedulerIntervalSeconds"/> it fires due schedule and poll
/// triggers and scheduled imports (as the system actor "Scheduler"); once per hour it removes history older than the
/// retention period.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="settings">The database settings.</param>
/// <param name="options">The engine options.</param>
/// <param name="logger">The logger.</param>
public sealed class SchedulerWorker(
    IServiceScopeFactory scopeFactory,
    DatabaseSettingsStore settings,
    IOptions<FlowEngineOptions> options,
    ILogger<SchedulerWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastCleanup = DateTimeOffset.MinValue;
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.SchedulerIntervalSeconds, 5, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                if (!settings.IsConfigured)
                {
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Scheduler"));
                var processor = scope.ServiceProvider.GetRequiredService<ScheduleProcessor>();
                var fired = await processor.ProcessDueAsync(stoppingToken);
                if (fired > 0)
                {
                    logger.LogDebug("Scheduler fired {Count} job(s).", fired);
                }

                if (DateTimeOffset.UtcNow - lastCleanup > TimeSpan.FromHours(1))
                {
                    lastCleanup = DateTimeOffset.UtcNow;
                    var removed = await processor.CleanupAsync(stoppingToken);
                    if (removed > 0)
                    {
                        logger.LogInformation("Automation retention removed {Count} history entries.", removed);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler tick failed.");
            }
        }
    }
}
