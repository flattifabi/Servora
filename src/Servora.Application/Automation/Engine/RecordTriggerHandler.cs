using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Servora.Application.Automation.Actions;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Application.Tables;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Engine;

/// <summary>
/// Starts record-triggered flows (created / updated + condition) for committed record changes. The idempotency key is
/// derived from the event so redelivery never starts a flow twice, changes made by the same flow do not re-trigger it,
/// and the record snapshot is reduced to exposed fields (no <c>[NotExposed]</c> values) and redacted.
/// </summary>
/// <param name="dbFactory">The database factory.</param>
/// <param name="starter">The run starter.</param>
/// <param name="evaluator">The condition evaluator.</param>
/// <param name="tables">The table registry.</param>
/// <param name="actorContext">The actor context (event cascade depth).</param>
/// <param name="cache">The memory cache.</param>
/// <param name="logger">The logger.</param>
public sealed class RecordTriggerHandler(
    IAppDbContextFactory dbFactory,
    IFlowRunStarter starter,
    IConditionEvaluator evaluator,
    ITableRegistry tables,
    ActorContext actorContext,
    IMemoryCache cache,
    ILogger<RecordTriggerHandler> logger) : IRecordEventHandler
{
    /// <summary>The cache key of the active record-triggered flows.</summary>
    public const string CacheKey = "automation.record-trigger-flows";

    /// <inheritdoc />
    public int Order => 500;

    /// <inheritdoc />
    public async Task HandleAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken)
    {
        if (recordEvent.ChangeType == RecordChangeType.Deleted)
        {
            return;
        }

        var flows = await GetRecordFlowsAsync(cancellationToken);
        var candidates = flows.Where(f => string.Equals(f.TriggerTable, recordEvent.TableName, StringComparison.OrdinalIgnoreCase) && Matches(f.TriggerType, recordEvent.ChangeType)).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var table = await tables.GetTableAsync(recordEvent.TableName, cancellationToken);
        var record = BuildRecord(recordEvent.Snapshot, table);
        foreach (var flow in candidates)
        {
            if (string.Equals(recordEvent.ActorName, $"Flow: {flow.Name}", StringComparison.Ordinal))
            {
                continue; // A flow never re-triggers itself.
            }

            var condition = ConditionSerializer.Deserialize(flow.TriggerConditionJson);
            if (!evaluator.Evaluate(condition, recordEvent.Snapshot, new ConditionContext(recordEvent.ActorUserId, recordEvent.Changes)))
            {
                continue;
            }

            var trigger = new JsonObject
            {
                ["table"] = recordEvent.TableName,
                ["recordId"] = recordEvent.RecordId.ToString(),
                ["changeType"] = recordEvent.ChangeType.ToString(),
                ["occurredAt"] = recordEvent.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
                ["actor"] = new JsonObject { ["name"] = recordEvent.ActorName, ["userId"] = recordEvent.ActorUserId?.ToString() },
                ["record"] = record.DeepClone(),
                ["changes"] = BuildChanges(recordEvent, table),
            };

            var display = table is null ? null : record[table.DisplayField]?.ToString();
            var result = await starter.StartAsync(
                new FlowStartRequest
                {
                    FlowId = flow.Id,
                    TriggerType = flow.TriggerType,
                    IdempotencyKey = $"rec:{recordEvent.TableName}:{recordEvent.RecordId:N}:{(int)recordEvent.ChangeType}:{recordEvent.OccurredAt.UtcTicks}",
                    Trigger = trigger,
                    Summary = $"{table?.Label ?? recordEvent.TableName} {display ?? recordEvent.RecordId.ToString()} {recordEvent.ChangeType.ToString().ToLowerInvariant()}",
                    TriggerTable = recordEvent.TableName,
                    TriggerRecordId = recordEvent.RecordId,
                    EventDepth = actorContext.EventDepth,
                    StartedBy = recordEvent.ActorName,
                },
                cancellationToken);
            if (!result.Succeeded)
            {
                logger.LogWarning("Flow {Flow} could not be started for {Table}/{RecordId}: {Error}", flow.Name, recordEvent.TableName, recordEvent.RecordId, result.Error);
            }
        }
    }

    /// <summary>Builds the exposed, redacted record snapshot used as <c>trigger.record</c>.</summary>
    /// <param name="snapshot">The raw snapshot.</param>
    /// <param name="table">The table definition, if known.</param>
    /// <returns>The record object.</returns>
    public static JsonObject BuildRecord(IReadOnlyDictionary<string, object?> snapshot, TableDefinition? table)
    {
        var filtered = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in snapshot)
        {
            var exposed = table is null ? !FlowRedactor.IsSensitiveName(pair.Key) : pair.Key == "Id" || table.FindField(pair.Key) is not null;
            if (exposed)
            {
                filtered[pair.Key] = pair.Value;
            }
        }

        return (JsonObject)FlowRedactor.Redact(FlowValues.ToJsonObject(filtered))!;
    }

    private static JsonArray BuildChanges(RecordChangedEvent recordEvent, TableDefinition? table)
    {
        var changes = new JsonArray();
        foreach (var change in recordEvent.Changes.Take(200))
        {
            if (table is not null && table.FindField(change.Field) is null)
            {
                continue;
            }

            var values = FlowValues.ToJsonObject(new Dictionary<string, object?> { ["old"] = change.OldValue, ["new"] = change.NewValue });
            changes.Add(new JsonObject
            {
                ["field"] = change.Field,
                ["old"] = FlowRedactor.IsSensitiveName(change.Field) ? FlowRedactor.Mask : values["old"]?.DeepClone(),
                ["new"] = FlowRedactor.IsSensitiveName(change.Field) ? FlowRedactor.Mask : values["new"]?.DeepClone(),
            });
        }

        return changes;
    }

    private static bool Matches(FlowTriggerType trigger, RecordChangeType change) => trigger switch
    {
        FlowTriggerType.RecordCreated => change == RecordChangeType.Created,
        FlowTriggerType.RecordUpdated => change == RecordChangeType.Updated,
        FlowTriggerType.RecordCreatedOrUpdated => change is RecordChangeType.Created or RecordChangeType.Updated,
        _ => false,
    };

    private async Task<IReadOnlyList<Flow>> GetRecordFlowsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<Flow>? cached) && cached is not null)
        {
            return cached;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var flows = await db.Set<Flow>().AsNoTracking()
            .Where(f => f.IsActive && f.TriggerTable != null
                && (f.TriggerType == FlowTriggerType.RecordCreated || f.TriggerType == FlowTriggerType.RecordUpdated || f.TriggerType == FlowTriggerType.RecordCreatedOrUpdated))
            .OrderBy(f => f.Name)
            .Take(500)
            .Select(f => new Flow { Id = f.Id, Name = f.Name, TriggerType = f.TriggerType, TriggerTable = f.TriggerTable, TriggerConditionJson = f.TriggerConditionJson })
            .ToListAsync(cancellationToken);
        cache.Set(CacheKey, (IReadOnlyList<Flow>)flows, TimeSpan.FromSeconds(30));
        return flows;
    }
}
