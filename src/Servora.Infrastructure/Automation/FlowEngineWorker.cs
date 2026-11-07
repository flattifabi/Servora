using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Servora.Application.Automation.Engine;
using Servora.Application.Common;
using Servora.Domain.Automation;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Automation;

/// <summary>
/// Background flow engine: executes queued runs with bounded concurrency (<see cref="FlowEngineOptions.MaxConcurrentRuns"/>),
/// each in its own DI scope as the flow's system actor. On start it requeues runs that were interrupted by a restart
/// and periodically sweeps queued runs that did not fit into the channel.
/// </summary>
/// <param name="queue">The run queue.</param>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="settings">The database settings (the engine idles until a database is configured).</param>
/// <param name="options">The engine options.</param>
/// <param name="logger">The logger.</param>
public sealed class FlowEngineWorker(
    FlowRunQueue queue,
    IServiceScopeFactory scopeFactory,
    DatabaseSettingsStore settings,
    IOptions<FlowEngineOptions> options,
    ILogger<FlowEngineWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!settings.IsConfigured && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        var sweeper = SweepLoopAsync(stoppingToken);
        var slots = new SemaphoreSlim(Math.Clamp(options.Value.MaxConcurrentRuns, 1, 32));
        try
        {
            await foreach (var runId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await slots.WaitAsync(stoppingToken);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteRunAsync(runId, stoppingToken);
                    }
                    finally
                    {
                        slots.Release();
                    }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown.
        }

        await sweeper;
    }

    private async Task ExecuteRunAsync(Guid runId, CancellationToken stoppingToken)
    {
        using var execution = queue.BeginExecution(runId, stoppingToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Flow engine"));
            var runner = scope.ServiceProvider.GetRequiredService<FlowRunner>();
            await runner.ExecuteAsync(runId, execution.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown; the run is requeued on the next start.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flow run {RunId} could not be executed.", runId);
        }
        finally
        {
            queue.EndExecution(runId);
        }
    }

    private async Task SweepLoopAsync(CancellationToken stoppingToken)
    {
        var first = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(recoverInterrupted: first, stoppingToken);
                first = false;
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.ResumePollSeconds, 2, 300)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Flow engine sweep failed.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            }
        }
    }

    private async Task SweepAsync(bool recoverInterrupted, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Flow engine"));
        var dbFactory = scope.ServiceProvider.GetRequiredService<IAppDbContextFactory>();
        var now = DateTimeOffset.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
        if (recoverInterrupted)
        {
            // Runs that were executing when the process stopped continue by replaying their completed steps.
            var interrupted = await db.Set<FlowRun>().Where(r => r.Status == FlowRunStatus.Running).Take(1000).ToListAsync(stoppingToken);
            foreach (var run in interrupted)
            {
                run.Status = FlowRunStatus.Queued;
            }

            if (interrupted.Count > 0)
            {
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Requeued {Count} interrupted flow run(s).", interrupted.Count);
            }
        }

        var queued = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.Status == FlowRunStatus.Queued)
            .OrderBy(r => r.QueuedAt)
            .Select(r => r.Id)
            .Take(500)
            .ToListAsync(stoppingToken);
        var due = await db.Set<FlowRun>().AsNoTracking()
            .Where(r => r.Status == FlowRunStatus.Waiting && r.ResumeAt != null && r.ResumeAt <= now)
            .OrderBy(r => r.ResumeAt)
            .Select(r => r.Id)
            .Take(200)
            .ToListAsync(stoppingToken);
        foreach (var id in queued.Concat(due))
        {
            queue.Enqueue(id);
        }
    }
}
