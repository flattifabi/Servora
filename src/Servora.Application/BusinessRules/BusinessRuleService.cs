using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.BusinessRules;
using Servora.Domain.Notifications;

namespace Servora.Application.BusinessRules;

/// <summary>Default <see cref="IBusinessRuleService"/>: validates, audits and invalidates the rule cache.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="access">The table access service (field write restrictions).</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="cache">The rule cache.</param>
/// <param name="audit">The audit service.</param>
public sealed class BusinessRuleService(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    ITableAccessService access,
    ICurrentUser currentUser,
    BusinessRuleCache cache,
    IAuditService audit) : IBusinessRuleService
{
    /// <summary>The maximum number of actions per rule.</summary>
    public const int MaxActions = 20;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<BusinessRuleModel>>> ListAsync(string? tableName = null, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageBusinessRules, cancellationToken))
        {
            return Result<IReadOnlyList<BusinessRuleModel>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<BusinessRule>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            query = query.Where(r => r.TableName == tableName);
        }

        var rules = await query.OrderBy(r => r.TableName).ThenBy(r => r.Order).ThenBy(r => r.Name).Take(1000).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<BusinessRuleModel>>.Success(rules.Select(ToModel).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<BusinessRuleModel>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageBusinessRules, cancellationToken))
        {
            return Result<BusinessRuleModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rule = await db.Set<BusinessRule>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return rule is null ? Result<BusinessRuleModel>.NotFound("The business rule was not found.") : Result<BusinessRuleModel>.Success(ToModel(rule));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(BusinessRuleModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageBusinessRules, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await ValidateAsync(db, model, cancellationToken) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        BusinessRule? rule;
        if (model.Id == Guid.Empty)
        {
            rule = new BusinessRule();
            db.Set<BusinessRule>().Add(rule);
        }
        else
        {
            rule = await db.Set<BusinessRule>().FirstOrDefaultAsync(r => r.Id == model.Id, cancellationToken);
            if (rule is null)
            {
                return Result<Guid>.NotFound("The business rule was not found.");
            }
        }

        var table = (await registry.GetTableAsync(model.TableName, cancellationToken))!;
        rule.Name = model.Name.Trim();
        rule.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        rule.TableName = table.Name;
        rule.IsActive = model.IsActive;
        rule.Timing = model.Timing;
        rule.OnInsert = model.OnInsert;
        rule.OnUpdate = model.OnUpdate;
        rule.OnDelete = model.OnDelete;
        rule.Order = model.Order;
        rule.ConditionJson = ConditionSerializer.Serialize(model.Condition);
        rule.ActionsJson = JsonSerializer.Serialize(model.Actions, BusinessRuleCache.JsonOptions);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Guid>.Conflict("The business rule was changed by someone else. Reload it and try again.");
        }

        cache.Invalidate();
        await audit.WriteAsync(new AuditEntry("platform", model.Id == Guid.Empty ? "businessrule.created" : "businessrule.updated")
        {
            TargetType = "sys_script",
            TargetId = rule.Id.ToString(),
            TargetName = rule.Name,
        }, cancellationToken);
        return Result<Guid>.Success(rule.Id);
    }

    /// <inheritdoc />
    public async Task<Result> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageBusinessRules, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rule = await db.Set<BusinessRule>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return Result.NotFound("The business rule was not found.");
        }

        rule.IsActive = active;
        await db.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        await audit.WriteAsync(new AuditEntry("platform", active ? "businessrule.activated" : "businessrule.deactivated")
        {
            TargetType = "sys_script",
            TargetId = rule.Id.ToString(),
            TargetName = rule.Name,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ManageBusinessRules, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rule = await db.Set<BusinessRule>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return Result.NotFound("The business rule was not found.");
        }

        db.Set<BusinessRule>().Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        await audit.WriteAsync(new AuditEntry("platform", "businessrule.deleted")
        {
            TargetType = "sys_script",
            TargetId = rule.Id.ToString(),
            TargetName = rule.Name,
        }, cancellationToken);
        return Result.Success();
    }

    private static BusinessRuleModel ToModel(BusinessRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Description = rule.Description,
        TableName = rule.TableName,
        IsActive = rule.IsActive,
        Timing = rule.Timing,
        OnInsert = rule.OnInsert,
        OnUpdate = rule.OnUpdate,
        OnDelete = rule.OnDelete,
        Order = rule.Order,
        Condition = ConditionSerializer.Deserialize(rule.ConditionJson),
        Actions = BusinessRuleCache.ParseActions(rule.ActionsJson),
        UpdatedAt = rule.UpdatedAt,
        UpdatedBy = rule.UpdatedBy,
    };

    private async Task<string?> ValidateAsync(IAppDbContext db, BusinessRuleModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 150)
        {
            return "The name is required and must not exceed 150 characters.";
        }

        if (model.Description is { Length: > 2000 })
        {
            return "The description must not exceed 2000 characters.";
        }

        if (!Enum.IsDefined(model.Timing))
        {
            return "Select when the rule runs.";
        }

        var table = await registry.GetTableAsync(model.TableName ?? string.Empty, cancellationToken);
        if (table is null)
        {
            return "Select an existing table.";
        }

        if (!model.OnInsert && !model.OnUpdate && !model.OnDelete)
        {
            return "Select at least one of insert, update or delete.";
        }

        if (model.Order is < 0 or > 1_000_000)
        {
            return "The order must be between 0 and 1,000,000.";
        }

        if (ConditionValidator.Validate(table, model.Condition) is { } conditionError)
        {
            return conditionError;
        }

        if (model.Actions.Count == 0 || model.Actions.Count > MaxActions)
        {
            return $"A business rule needs between 1 and {MaxActions} actions.";
        }

        var before = model.Timing == BusinessRuleTiming.Before;
        foreach (var action in model.Actions)
        {
            switch (action.Type)
            {
                case BusinessRuleActionType.SetField:
                    var field = table.FindField(action.Field ?? string.Empty);
                    if (field is null || !access.IsFieldWritable(table, field) || TablePolicies.Find(table.Name) is { Write: null })
                    {
                        return $"'{action.Field}' is not a writable field of {table.Label}.";
                    }

                    if (action.Value is { Length: > 4000 })
                    {
                        return "Field values must not exceed 4000 characters.";
                    }

                    if (!RecordValueConverter.TryConvert(action.Value, field, RecordEntityAccessor.GetClrType(table, field), out _, out var valueError))
                    {
                        return valueError;
                    }

                    action.Field = field.Name;
                    break;
                case BusinessRuleActionType.Abort:
                    if (!before)
                    {
                        return "Abort actions are only available in before rules.";
                    }

                    if (string.IsNullOrWhiteSpace(action.Message) || action.Message.Length > 500)
                    {
                        return "An abort action needs a message of at most 500 characters.";
                    }

                    break;
                case BusinessRuleActionType.AddWorkNote:
                    if (string.IsNullOrWhiteSpace(action.Message) || action.Message.Length > 4000)
                    {
                        return "A work note action needs a text of at most 4000 characters.";
                    }

                    break;
                case BusinessRuleActionType.StartFlow:
                    if (before)
                    {
                        return "Flows can only be started by after rules.";
                    }

                    if (action.TargetId is null || action.TargetId == Guid.Empty)
                    {
                        return "Select the flow to start.";
                    }

                    break;
                case BusinessRuleActionType.SendNotification:
                    if (before)
                    {
                        return "Notifications can only be sent by after rules.";
                    }

                    var notificationId = action.TargetId ?? Guid.Empty;
                    var notification = await db.Set<NotificationDefinition>().AsNoTracking().FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
                    if (notification is null || !string.Equals(notification.TableName, table.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return "Select a notification of the same table.";
                    }

                    break;
                default:
                    return "Unknown action type.";
            }
        }

        return null;
    }
}
