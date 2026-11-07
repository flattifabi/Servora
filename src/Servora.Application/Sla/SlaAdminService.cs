using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Notifications;
using Servora.Domain.Sla;

namespace Servora.Application.Sla;

/// <summary>Default <see cref="ISlaAdminService"/>.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class SlaAdminService(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    ICurrentUser currentUser,
    IAuditService audit,
    IClock clock) : ISlaAdminService
{
    /// <summary>The maximum target duration (one year).</summary>
    public const int MaxTargetMinutes = 525_600;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SlaDefinitionModel>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<SlaDefinitionModel>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var definitions = await db.Set<SlaDefinition>().AsNoTracking().OrderBy(d => d.TableName).ThenBy(d => d.Name).Take(500).ToListAsync(cancellationToken);
        var counts = await db.Set<TaskSla>().AsNoTracking()
            .Where(t => t.Stage == SlaStage.InProgress || t.Stage == SlaStage.Paused)
            .GroupBy(t => t.SlaDefinitionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        return Result<IReadOnlyList<SlaDefinitionModel>>.Success(definitions.Select(d =>
        {
            var model = ToModel(d);
            model.ActiveTimers = counts.GetValueOrDefault(d.Id);
            return model;
        }).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<SlaDefinitionModel>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<SlaDefinitionModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var definition = await db.Set<SlaDefinition>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (definition is null)
        {
            return Result<SlaDefinitionModel>.NotFound("The SLA definition was not found.");
        }

        var model = ToModel(definition);
        model.ActiveTimers = await db.Set<TaskSla>().CountAsync(t => t.SlaDefinitionId == id && (t.Stage == SlaStage.InProgress || t.Stage == SlaStage.Paused), cancellationToken);
        return Result<SlaDefinitionModel>.Success(model);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(SlaDefinitionModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var table = await registry.GetTableAsync(model.TableName ?? string.Empty, cancellationToken);
        if (await ValidateAsync(db, model, table, cancellationToken) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        SlaDefinition? definition;
        if (model.Id == Guid.Empty)
        {
            definition = new SlaDefinition();
            db.Set<SlaDefinition>().Add(definition);
        }
        else
        {
            definition = await db.Set<SlaDefinition>().FirstOrDefaultAsync(d => d.Id == model.Id, cancellationToken);
            if (definition is null)
            {
                return Result<Guid>.NotFound("The SLA definition was not found.");
            }

            if (!string.Equals(definition.TableName, table!.Name, StringComparison.OrdinalIgnoreCase)
                && await db.Set<TaskSla>().AnyAsync(t => t.SlaDefinitionId == definition.Id, cancellationToken))
            {
                return Result<Guid>.Invalid("The table of an SLA definition with timers cannot be changed.");
            }
        }

        definition.Name = model.Name.Trim();
        definition.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        definition.TableName = table!.Name;
        definition.IsActive = model.IsActive;
        definition.TargetMinutes = model.TargetMinutes;
        definition.StartConditionJson = ConditionSerializer.Serialize(model.StartCondition);
        definition.PauseConditionJson = ConditionSerializer.Serialize(model.PauseCondition);
        definition.StopConditionJson = ConditionSerializer.Serialize(model.StopCondition);
        definition.CancelConditionJson = ConditionSerializer.Serialize(model.CancelCondition);
        definition.ScheduleType = model.ScheduleType;
        definition.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? "UTC" : model.TimeZoneId.Trim();
        definition.BusinessDays = string.Join(',', model.BusinessDays.Distinct().OrderBy(d => d).Select(d => ((int)d).ToString(CultureInfo.InvariantCulture)));
        definition.DayStartMinute = model.DayStartMinute;
        definition.DayEndMinute = model.DayEndMinute;
        definition.BreachNotificationId = model.BreachNotificationId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("platform", model.Id == Guid.Empty ? "sla.created" : "sla.updated")
        {
            TargetType = "contract_sla",
            TargetId = definition.Id.ToString(),
            TargetName = definition.Name,
        }, cancellationToken);
        return Result<Guid>.Success(definition.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var definition = await db.Set<SlaDefinition>().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (definition is null)
        {
            return Result.NotFound("The SLA definition was not found.");
        }

        await db.Set<TaskSla>().Where(t => t.SlaDefinitionId == id).ExecuteDeleteAsync(cancellationToken);
        db.Set<SlaDefinition>().Remove(definition);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("platform", "sla.deleted") { TargetType = "contract_sla", TargetId = id.ToString(), TargetName = definition.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<SlaOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<SlaOverview>.Forbidden();
        }

        var now = clock.UtcNow;
        var since = now.AddDays(-30);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var running = db.Set<TaskSla>().AsNoTracking().Where(t => t.Stage == SlaStage.InProgress || t.Stage == SlaStage.Paused);
        var runningCount = await running.CountAsync(cancellationToken);
        var runningBreached = await running.CountAsync(t => t.HasBreached, cancellationToken);
        var completed = db.Set<TaskSla>().AsNoTracking().Where(t => t.Stage == SlaStage.Completed && t.StoppedAt >= since);
        var completedCount = await completed.CountAsync(cancellationToken);
        var achieved = await completed.CountAsync(t => !t.HasBreached, cancellationToken);

        var urgent = await running.OrderByDescending(t => t.HasBreached).ThenBy(t => t.DueAt).Take(50).ToListAsync(cancellationToken);
        var ids = urgent.Select(t => t.SlaDefinitionId).Distinct().ToList();
        var definitions = await db.Set<SlaDefinition>().AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
        var rows = new List<SlaTimerRow>(urgent.Count);
        foreach (var timer in urgent)
        {
            var definition = definitions.GetValueOrDefault(timer.SlaDefinitionId);
            var schedule = definition is null ? SlaSchedule.TwentyFourSeven : SlaSchedule.From(definition);
            var table = await registry.GetTableAsync(timer.TableName, cancellationToken);
            rows.Add(new SlaTimerRow(
                timer.Id,
                definition?.Name ?? "SLA",
                timer.TableName,
                timer.RecordId,
                timer.RecordDisplay,
                SlaTimer.StageName(timer),
                timer.StartedAt,
                timer.DueAt,
                SlaTimer.PercentElapsed(timer, schedule, definition?.TargetMinutes ?? 0, now),
                timer.HasBreached,
                table is null ? $"/table/{timer.TableName}/{timer.RecordId}" : RecordUrls.For(table, timer.RecordId)));
        }

        return Result<SlaOverview>.Success(new SlaOverview(runningCount, runningBreached, completedCount, achieved, rows));
    }

    private static SlaDefinitionModel ToModel(SlaDefinition definition) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        Description = definition.Description,
        TableName = definition.TableName,
        IsActive = definition.IsActive,
        TargetMinutes = definition.TargetMinutes,
        StartCondition = ConditionSerializer.Deserialize(definition.StartConditionJson),
        PauseCondition = ConditionSerializer.Deserialize(definition.PauseConditionJson),
        StopCondition = ConditionSerializer.Deserialize(definition.StopConditionJson),
        CancelCondition = ConditionSerializer.Deserialize(definition.CancelConditionJson),
        ScheduleType = definition.ScheduleType,
        TimeZoneId = definition.TimeZoneId,
        BusinessDays = [.. SlaSchedule.ParseDays(definition.BusinessDays)],
        DayStartMinute = definition.DayStartMinute,
        DayEndMinute = definition.DayEndMinute,
        BreachNotificationId = definition.BreachNotificationId,
        UpdatedAt = definition.UpdatedAt,
    };

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) =>
        currentUser.HasPermissionAsync(Permissions.Platform.ManageSla, cancellationToken);

    private static async Task<string?> ValidateAsync(IAppDbContext db, SlaDefinitionModel model, TableDefinition? table, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 150)
        {
            return "The name is required and must not exceed 150 characters.";
        }

        if (model.Description is { Length: > 2000 })
        {
            return "The description must not exceed 2000 characters.";
        }

        if (table is null)
        {
            return "Select an existing table.";
        }

        if (model.TargetMinutes is < 1 or > MaxTargetMinutes)
        {
            return $"The target must be between 1 and {MaxTargetMinutes} minutes.";
        }

        foreach (var (label, condition) in new[] { ("start", model.StartCondition), ("pause", model.PauseCondition), ("stop", model.StopCondition), ("cancel", model.CancelCondition) })
        {
            if (ConditionValidator.Validate(table, condition, allowChangeOperators: false) is { } error)
            {
                return $"Invalid {label} condition: {error}";
            }
        }

        if (!Enum.IsDefined(model.ScheduleType))
        {
            return "Select a schedule.";
        }

        if (model.ScheduleType == SlaScheduleType.BusinessHours)
        {
            if (string.IsNullOrWhiteSpace(model.TimeZoneId) || !TimeZoneInfo.TryFindSystemTimeZoneById(model.TimeZoneId.Trim(), out _))
            {
                return "Select a valid time zone.";
            }

            if (model.BusinessDays.Count == 0 || model.BusinessDays.Any(d => !Enum.IsDefined(d)))
            {
                return "Select at least one business day.";
            }

            if (model.DayStartMinute is < 0 or > 1439 || model.DayEndMinute is < 1 or > 1440 || model.DayEndMinute <= model.DayStartMinute)
            {
                return "The business day must end after it starts.";
            }
        }

        if (model.BreachNotificationId is { } notificationId)
        {
            var notification = await db.Set<NotificationDefinition>().AsNoTracking().FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
            if (notification is null || !string.Equals(notification.TableName, table.Name, StringComparison.OrdinalIgnoreCase))
            {
                return "The breach notification must belong to the same table.";
            }
        }

        return null;
    }
}
