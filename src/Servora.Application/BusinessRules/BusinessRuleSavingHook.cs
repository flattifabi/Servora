using Microsoft.Extensions.Logging;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Application.Notifications;
using Servora.Application.Tables;
using Servora.Domain.Activity;
using Servora.Domain.BusinessRules;

namespace Servora.Application.BusinessRules;

/// <summary>
/// Runs "before" business rules inside <c>SaveChangesAsync</c>: matching rules set field values on the pending record,
/// add work notes or abort the save with a user-safe message.
/// </summary>
/// <param name="dbFactory">The database context factory (rule loading only).</param>
/// <param name="cache">The rule cache.</param>
/// <param name="registry">The table registry.</param>
/// <param name="evaluator">The condition evaluator.</param>
/// <param name="activity">The activity service.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="logger">The logger.</param>
public sealed class BusinessRuleSavingHook(
    IAppDbContextFactory dbFactory,
    BusinessRuleCache cache,
    ITableRegistry registry,
    IConditionEvaluator evaluator,
    IActivityService activity,
    ICurrentUser currentUser,
    ILogger<BusinessRuleSavingHook> logger) : IRecordSavingHook
{
    /// <inheritdoc />
    public int Order => 50;

    /// <inheritdoc />
    public async Task OnSavingAsync(RecordSavingContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<CompiledBusinessRule> rules;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            rules = await cache.GetAsync(db, context.TableName, cancellationToken);
        }

        var applicable = rules.Where(r => r.Timing == BusinessRuleTiming.Before && r.AppliesTo(context.ChangeType)).ToList();
        if (applicable.Count == 0)
        {
            return;
        }

        var table = await registry.GetTableAsync(context.TableName, cancellationToken);
        if (table is null)
        {
            return;
        }

        var values = RecordEntityAccessor.ReadValues(table, context.Entity);
        var conditionContext = new ConditionContext(currentUser.UserId, RecordChangeExpander.Expand(context.Changes));
        var recordId = (context.Entity as Domain.Common.Entity)?.Id ?? Guid.Empty;
        foreach (var rule in applicable)
        {
            if (!evaluator.Evaluate(rule.Condition, values, conditionContext))
            {
                continue;
            }

            foreach (var action in rule.Actions)
            {
                switch (action.Type)
                {
                    case BusinessRuleActionType.SetField when context.ChangeType != RecordChangeType.Deleted:
                        var field = table.FindField(action.Field ?? string.Empty);
                        if (field is null || field.IsReadOnly)
                        {
                            break;
                        }

                        if (RecordValueConverter.TryConvert(action.Value, field, RecordEntityAccessor.GetClrType(table, field), out var value, out var error)
                            && RecordEntityAccessor.WriteValue(context.Entity, field, value))
                        {
                            values[field.Name] = value;
                        }
                        else
                        {
                            logger.LogWarning("Business rule {Rule} could not set {Field}: {Error}", rule.Name, action.Field, error);
                        }

                        break;
                    case BusinessRuleActionType.Abort:
                        var message = TemplateRenderer.RenderLine(action.Message, values);
                        logger.LogInformation("Business rule {Rule} aborted a {ChangeType} of {Table}.", rule.Name, context.ChangeType, context.TableName);
                        context.Abort(string.IsNullOrWhiteSpace(message) ? $"The change was rejected by the business rule '{rule.Name}'." : message);
                        return;
                    case BusinessRuleActionType.AddWorkNote when context.ChangeType != RecordChangeType.Deleted && recordId != Guid.Empty:
                        var text = TemplateRenderer.RenderText(action.Message, values);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            await activity.AddAsync(table.Name, recordId, ActivityType.WorkNote, text, cancellationToken);
                        }

                        break;
                }
            }
        }
    }
}
