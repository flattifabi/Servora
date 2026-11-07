using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servora.Application.Api;
using Servora.Application.Common;
using Servora.Domain.Api;
using Servora.Domain.Mcp;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Api;

/// <summary>
/// Background service that flushes aggregated API key usage every few seconds and prunes old MCP invocation logs and
/// usage rows once per hour (bounded batches, cancellation-aware, final flush on shutdown).
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="tracker">The usage tracker.</param>
/// <param name="settings">The database settings (nothing is written in setup mode).</param>
/// <param name="logger">The logger.</param>
public sealed class ApiUsageMaintenanceService(
    IServiceScopeFactory scopeFactory,
    ApiKeyUsageTracker tracker,
    DatabaseSettingsStore settings,
    ILogger<ApiUsageMaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan s_flushInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_pruneInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan s_invocationRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan s_usageRetention = TimeSpan.FromDays(400);
    private DateTimeOffset _lastPrune = DateTimeOffset.MinValue;

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await RunOnceAsync(prune: false, CancellationToken.None);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(s_flushInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var prune = DateTimeOffset.UtcNow - _lastPrune > s_pruneInterval;
                await RunOnceAsync(prune, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    private async Task RunOnceAsync(bool prune, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured || (tracker.PendingCount == 0 && !prune))
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("API usage tracker"));
            await using var db = await scope.ServiceProvider.GetRequiredService<IAppDbContextFactory>().CreateDbContextAsync(cancellationToken);
            await tracker.FlushAsync(db, cancellationToken);

            if (prune)
            {
                _lastPrune = DateTimeOffset.UtcNow;
                var invocationCutoff = DateTimeOffset.UtcNow - s_invocationRetention;
                var usageCutoff = DateTimeOffset.UtcNow - s_usageRetention;
                var removed = await db.Set<McpInvocation>().Where(i => i.OccurredAt < invocationCutoff).ExecuteDeleteAsync(cancellationToken);
                removed += await db.Set<ApiKeyUsageDay>().Where(u => u.Day < usageCutoff).ExecuteDeleteAsync(cancellationToken);
                if (removed > 0)
                {
                    logger.LogInformation("Pruned {Count} old MCP invocation / API usage rows.", removed);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "API usage maintenance failed; it will be retried.");
        }
    }
}
