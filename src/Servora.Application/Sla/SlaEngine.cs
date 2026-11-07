using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Application.Notifications;
using Servora.Application.Tables;
using Servora.Domain.Sla;

namespace Servora.Application.Sla;

/// <summary>
/// Attaches, pauses, resumes, completes and cancels SLA timers when records change, and detects breaches of running
/// timers (called periodically by the SLA evaluator background service). All operations are bounded.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="evaluator">The condition evaluator.</param>
/// <param name="records">The record service (breach notification data).</param>
/// <param name="activity">The activity service.</param>
/// <param name="notifications">The notification dispatcher.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class SlaEngine(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    IConditionEvaluator evaluator,
    IRecordService records,
    IActivityService activity,
    INotificationDispatcher notifications,
    IClock clock,
    ILogger<SlaEngine> logger)
{
    private const int MaxDefinitionsPerTable = 50;
    private const int MaxBatchesPerRun = 10;

    /// <summary>Processes a committed record change.</summary>
    /// <param name="recordEvent">The record event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the timers are updated.</returns>
    public async Task ProcessEventAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ProcessEventCoreAsync(recordEvent, cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                // A breach evaluation updated the same timer concurrently; reload and apply the change again.
            }
        }
    }

    /// <summary>Marks running timers whose due date passed as breached.</summary>
    /// <param name="batchSize">The batch size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of breached timers.</returns>
    public async Task<int> EvaluateBreachesAsync(int batchSize, CancellationToken cancellationToken)
    {
        batchSize = Math.Clamp(batchSize, 1, 1000);
        var total = 0;
        for (var batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            var now = clock.UtcNow;
            List<TaskSla> due;
            Dictionary<Guid, SlaDefinition> definitions;
            await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            {
                due = await db.Set<TaskSla>()
                    .Where(t => t.Stage == SlaStage.InProgress && !t.HasBreached && t.DueAt != null && t.DueAt <= now)
                    .OrderBy(t => t.DueAt)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
                if (due.Count == 0)
                {
                    break;
                }

                var definitionIds = due.Select(t => t.SlaDefinitionId).Distinct().ToList();
                definitions = await db.Set<SlaDefinition>().AsNoTracking().Where(d => definitionIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
                foreach (var sla in due)
                {
                    sla.HasBreached = true;
                    sla.BreachedAt = now;
                }

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another process changed one of the timers; it is picked up again in the next run.
                    logger.LogDebug("SLA breach batch skipped because of a concurrent update.");
                    continue;
                }
            }

            foreach (var sla in due)
            {
                await OnBreachedAsync(sla, definitions.GetValueOrDefault(sla.SlaDefinitionId), now, cancellationToken);
            }

            total += due.Count;
            if (due.Count < batchSize)
            {
                break;
            }
        }

        return total;
    }

    private async Task ProcessEventCoreAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var active = await db.Set<TaskSla>()
            .Where(t => t.TableName == recordEvent.TableName && t.RecordId == recordEvent.RecordId && (t.Stage == SlaStage.InProgress || t.Stage == SlaStage.Paused))
            .Take(MaxDefinitionsPerTable)
            .ToListAsync(cancellationToken);
        var now = recordEvent.OccurredAt;
        var messages = new List<string>();

        var activeDefinitionIds = active.Select(a => a.SlaDefinitionId).Distinct().ToList();
        var definitions = await db.Set<SlaDefinition>().AsNoTracking()
            .Where(d => d.TableName == recordEvent.TableName && (d.IsActive || activeDefinitionIds.Contains(d.Id)))
            .OrderBy(d => d.Name)
            .Take(MaxDefinitionsPerTable)
            .ToListAsync(cancellationToken);

        if (recordEvent.ChangeType == RecordChangeType.Deleted)
        {
            foreach (var sla in active)
            {
                var definition = definitions.FirstOrDefault(d => d.Id == sla.SlaDefinitionId);
                SlaTimer.Stop(sla, definition is null ? SlaSchedule.TwentyFourSeven : SlaSchedule.From(definition), definition?.TargetMinutes ?? 0, now, cancelled: true);
            }

            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (definitions.Count == 0)
        {
            return;
        }

        var table = await registry.GetTableAsync(recordEvent.TableName, cancellationToken);
        if (table is null)
        {
            return;
        }

        var values = RecordEntityAccessor.FilterExposed(table, recordEvent.Snapshot);
        var display = ConditionValueConverter.ToText(values.GetValueOrDefault(table.DisplayField));
        foreach (var definition in definitions)
        {
            var schedule = SlaSchedule.From(definition);
            var sla = active.FirstOrDefault(t => t.SlaDefinitionId == definition.Id);
            var start = evaluator.Evaluate(ConditionSerializer.Deserialize(definition.StartConditionJson), values);
            var stop = Matches(definition.StopConditionJson, values);
            var pause = Matches(definition.PauseConditionJson, values);
            var cancel = Matches(definition.CancelConditionJson, values);

            if (sla is null)
            {
                if (!definition.IsActive || !start || stop || cancel)
                {
                    continue;
                }

                sla = SlaTimer.Start(definition, schedule, table.Name, recordEvent.RecordId, display, now);
                db.Set<TaskSla>().Add(sla);
                if (pause)
                {
                    SlaTimer.Pause(sla, schedule, now);
                }

                messages.Add(pause
                    ? $"SLA '{definition.Name}' attached (paused)."
                    : $"SLA '{definition.Name}' started; due {sla.DueAt:yyyy-MM-dd HH:mm} UTC.");
                continue;
            }

            if (cancel)
            {
                SlaTimer.Stop(sla, schedule, definition.TargetMinutes, now, cancelled: true);
                messages.Add($"SLA '{definition.Name}' cancelled.");
            }
            else if (stop)
            {
                SlaTimer.Stop(sla, schedule, definition.TargetMinutes, now, cancelled: false);
                messages.Add(sla.HasBreached ? $"SLA '{definition.Name}' completed after the target (breached)." : $"SLA '{definition.Name}' achieved.");
            }
            else if (pause && sla.Stage == SlaStage.InProgress)
            {
                SlaTimer.Pause(sla, schedule, now);
                messages.Add($"SLA '{definition.Name}' paused.");
            }
            else if (!pause && sla.Stage == SlaStage.Paused)
            {
                SlaTimer.Resume(sla, schedule, definition.TargetMinutes, now);
                messages.Add($"SLA '{definition.Name}' resumed; due {sla.DueAt:yyyy-MM-dd HH:mm} UTC.");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var message in messages)
        {
            await activity.AddSystemAsync(recordEvent.TableName, recordEvent.RecordId, message, cancellationToken);
        }
    }

    private bool Matches(string? conditionJson, IReadOnlyDictionary<string, object?> values)
    {
        var condition = ConditionSerializer.Deserialize(conditionJson);
        return !condition.IsEmpty && evaluator.Evaluate(condition, values);
    }

    private async Task OnBreachedAsync(TaskSla sla, SlaDefinition? definition, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var name = definition?.Name ?? "SLA";
        try
        {
            await activity.AddSystemAsync(sla.TableName, sla.RecordId, $"SLA '{name}' breached (target {definition?.TargetMinutes ?? 0} min).", cancellationToken);
            if (definition?.BreachNotificationId is not { } notificationId)
            {
                return;
            }

            var record = await records.GetAsync(sla.TableName, sla.RecordId, cancellationToken);
            if (!record.Succeeded)
            {
                return;
            }

            var recordEvent = new RecordChangedEvent(sla.TableName, sla.RecordId, RecordChangeType.Updated, [], record.Value!, null, "SLA engine", now, 0);
            await notifications.SendAsync(notificationId, recordEvent, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Breach handling of SLA {Sla} failed for {Table}/{RecordId}.", name, sla.TableName, sla.RecordId);
        }
    }
}
