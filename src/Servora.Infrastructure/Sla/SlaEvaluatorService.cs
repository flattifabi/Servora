using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servora.Application.Common;
using Servora.Application.Platform;
using Servora.Application.Sla;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Sla;

/// <summary>
/// Background service that periodically detects SLA breaches (bounded batches, configurable interval) and runs the
/// breach handling (activity entry, breach notification) as the system actor <c>SLA engine</c>.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="settings">The database settings (the evaluator idles until a database is configured).</param>
/// <param name="options">The platform options.</param>
/// <param name="logger">The logger.</param>
public sealed class SlaEvaluatorService(
    IServiceScopeFactory scopeFactory,
    DatabaseSettingsStore settings,
    IOptions<PlatformOptions> options,
    ILogger<SlaEvaluatorService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.SlaEvaluationIntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        do
        {
            if (!settings.IsConfigured)
            {
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("SLA engine"));
                var engine = scope.ServiceProvider.GetRequiredService<SlaEngine>();
                var breached = await engine.EvaluateBreachesAsync(options.Value.SlaBatchSize, stoppingToken);
                if (breached > 0)
                {
                    logger.LogInformation("{Count} SLA timer(s) breached.", breached);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA evaluation failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
