using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Identity.Ldap;
using Servora.Domain.Identity;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// Background scheduler for the directory synchronisation. Checks once a minute whether synchronisation is enabled
/// and the configured interval has elapsed since the last run, then runs it as the system actor. Does nothing while
/// the database is not configured or the directory integration is disabled.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="databaseSettings">The database settings store.</param>
/// <param name="logger">The logger.</param>
public sealed class DirectorySyncHostedService(
    IServiceScopeFactory scopeFactory,
    DatabaseSettingsStore databaseSettings,
    ILogger<DirectorySyncHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan s_tick = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Give the host time to finish database initialisation before the first check.
            await Task.Delay(s_tick, stoppingToken);
            using var timer = new PeriodicTimer(s_tick);
            do
            {
                await TickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        if (!databaseSettings.IsConfigured)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Directory synchronisation"));
            var settings = await scope.ServiceProvider.GetRequiredService<IDirectoryConfigurationProvider>().GetSettingsAsync(stoppingToken);
            if (!settings.Enabled || !settings.SyncEnabled || !settings.IsConnectionConfigured)
            {
                return;
            }

            var sync = scope.ServiceProvider.GetRequiredService<IDirectorySyncService>();
            var last = await sync.GetLastRunAsync(stoppingToken);
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            if (last is not null && last.StartedAt.AddMinutes(settings.SyncIntervalMinutes) > clock.UtcNow)
            {
                return;
            }

            var summary = await sync.RunAsync(DirectorySyncTrigger.Scheduled, stoppingToken);
            logger.LogInformation("Scheduled directory synchronisation finished with {Status}: {Message}", summary.Status, summary.Message);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled directory synchronisation failed.");
        }
    }
}
