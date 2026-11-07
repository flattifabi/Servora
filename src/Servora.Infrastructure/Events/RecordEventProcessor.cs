using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Events;

namespace Servora.Infrastructure.Events;

/// <summary>
/// Background service that dispatches queued record events to all <see cref="IRecordEventHandler"/>s. Each event is
/// processed in a fresh DI scope with a system actor; handler failures are logged and isolated.
/// </summary>
/// <param name="queue">The event queue.</param>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="logger">The logger.</param>
public sealed class RecordEventProcessor(RecordEventQueue queue, IServiceScopeFactory scopeFactory, ILogger<RecordEventProcessor> logger) : BackgroundService
{
    /// <summary>Maximum cascade depth. Events beyond this depth are dropped to stop rule / flow loops.</summary>
    public const int MaxDepth = 5;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var recordEvent in queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (recordEvent.Depth > MaxDepth)
            {
                logger.LogWarning("Dropped record event for {Table}/{RecordId}: cascade depth {Depth} exceeds {MaxDepth}.", recordEvent.TableName, recordEvent.RecordId, recordEvent.Depth, MaxDepth);
                continue;
            }

            using var scope = scopeFactory.CreateScope();
            var actor = scope.ServiceProvider.GetRequiredService<ActorContext>();
            actor.Set(ActorInfo.System);
            actor.EventDepth = recordEvent.Depth + 1;

            foreach (var handler in scope.ServiceProvider.GetServices<IRecordEventHandler>().OrderBy(h => h.Order))
            {
                try
                {
                    await handler.HandleAsync(recordEvent, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Record event handler {Handler} failed for {Table}/{RecordId} ({ChangeType}).", handler.GetType().Name, recordEvent.TableName, recordEvent.RecordId, recordEvent.ChangeType);
                }
            }
        }
    }
}
