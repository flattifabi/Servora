using System.Globalization;
using Microsoft.Extensions.Logging;
using Servora.Application.Activity;
using Servora.Application.Automation;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Application.Notifications;
using Servora.Application.Tables;
using Servora.Domain.Activity;
using Servora.Domain.BusinessRules;

namespace Servora.Application.BusinessRules;

/// <summary>
/// Runs "after" business rules for committed record events: work notes, follow-up field updates (through the record
/// service, so before rules and validation apply), flows and notifications. Runs as a system actor named after the rule.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="cache">The rule cache.</param>
/// <param name="registry">The table registry.</param>
/// <param name="evaluator">The condition evaluator.</param>
/// <param name="records">The record service.</param>
/// <param name="activity">The activity service.</param>
/// <param name="flows">The flow execution service.</param>
/// <param name="notifications">The notification dispatcher.</param>
/// <param name="actorContext">The actor context of the event scope.</param>
/// <param name="logger">The logger.</param>
public sealed class BusinessRuleEventHandler(
    IAppDbContextFactory dbFactory,
    BusinessRuleCache cache,
    ITableRegistry registry,
    IConditionEvaluator evaluator,
    IRecordService records,
    IActivityService activity,
    IFlowExecutionService flows,
    INotificationDispatcher notifications,
    ActorContext actorContext,
    ILogger<BusinessRuleEventHandler> logger) : IRecordEventHandler
{
    /// <inheritdoc />
    public int Order => 50;

    /// <inheritdoc />
    public async Task HandleAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken)
    {
        IReadOnlyList<CompiledBusinessRule> rules;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            rules = await cache.GetAsync(db, recordEvent.TableName, cancellationToken);
        }

        var applicable = rules.Where(r => r.Timing == BusinessRuleTiming.After && r.AppliesTo(recordEvent.ChangeType)).ToList();
        if (applicable.Count == 0)
        {
            return;
        }

        var table = await registry.GetTableAsync(recordEvent.TableName, cancellationToken);
        if (table is null)
        {
            return;
        }

        var values = RecordEntityAccessor.FilterExposed(table, recordEvent.Snapshot);
        var context = new ConditionContext(recordEvent.ActorUserId, RecordChangeExpander.Expand(recordEvent.Changes));
        var previousActor = actorContext.Override;
        try
        {
            foreach (var rule in applicable)
            {
                if (!evaluator.Evaluate(rule.Condition, values, context))
                {
                    continue;
                }

                actorContext.Set(ActorInfo.SystemNamed($"Business rule: {rule.Name}"));
                await RunActionsAsync(rule, table, recordEvent, values, cancellationToken);
            }
        }
        finally
        {
            actorContext.Set(previousActor ?? ActorInfo.System);
        }
    }

    private async Task RunActionsAsync(CompiledBusinessRule rule, TableDefinition table, RecordChangedEvent recordEvent, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var deleted = recordEvent.ChangeType == RecordChangeType.Deleted;
        var updates = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in rule.Actions)
        {
            try
            {
                switch (action.Type)
                {
                    case BusinessRuleActionType.SetField when !deleted && action.Field is not null:
                        updates[action.Field] = action.Value;
                        break;
                    case BusinessRuleActionType.AddWorkNote when !deleted:
                        var text = TemplateRenderer.RenderText(action.Message, values);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            await activity.AddAsync(table.Name, recordEvent.RecordId, ActivityType.WorkNote, text, cancellationToken);
                        }

                        break;
                    case BusinessRuleActionType.StartFlow when action.TargetId is { } flowId:
                        var started = await flows.StartAsync(flowId, new Dictionary<string, object?>
                        {
                            ["table"] = table.Name,
                            ["recordId"] = recordEvent.RecordId,
                            ["changeType"] = recordEvent.ChangeType.ToString(),
                            ["businessRule"] = rule.Name,
                        }, cancellationToken);
                        if (!started.Succeeded)
                        {
                            logger.LogWarning("Business rule {Rule} could not start flow {Flow}: {Error}", rule.Name, flowId, started.Error);
                        }

                        break;
                    case BusinessRuleActionType.SendNotification when action.TargetId is { } notificationId:
                        await notifications.SendAsync(notificationId, recordEvent, cancellationToken);
                        break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Business rule {Rule} action {Action} failed for {Table}/{RecordId}.", rule.Name, action.Type, table.Name, recordEvent.RecordId);
            }
        }

        if (updates.Count > 0)
        {
            var result = await records.UpdateAsync(table.Name, recordEvent.RecordId, updates, cancellationToken);
            if (!result.Succeeded)
            {
                logger.LogWarning("Business rule {Rule} could not update {Table}/{RecordId}: {Error}", rule.Name, table.Name, recordEvent.RecordId, result.Error);
                await activity.AddSystemAsync(table.Name, recordEvent.RecordId,
                    string.Create(CultureInfo.InvariantCulture, $"Business rule '{rule.Name}' could not update the record: {result.Error}"), cancellationToken);
            }
        }
    }
}
