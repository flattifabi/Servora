using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Common;
using Servora.Domain.Notifications;

namespace Servora.Application.Notifications;

/// <summary>Default <see cref="INotificationAdminService"/>.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="records">The record service (preview data, permission-checked).</param>
/// <param name="renderer">The renderer.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
public sealed class NotificationAdminService(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    IRecordService records,
    NotificationRenderer renderer,
    ICurrentUser currentUser,
    IAuditService audit) : INotificationAdminService
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<NotificationModel>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<NotificationModel>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Set<NotificationDefinition>().AsNoTracking().OrderBy(n => n.TableName).ThenBy(n => n.Name).Take(1000).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<NotificationModel>>.Success(items.Select(ToModel).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<NotificationModel>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<NotificationModel>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<NotificationDefinition>().AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        return item is null ? Result<NotificationModel>.NotFound("The notification was not found.") : Result<NotificationModel>.Success(ToModel(item));
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(NotificationModel model, CancellationToken cancellationToken = default)
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

        NotificationDefinition? item;
        if (model.Id == Guid.Empty)
        {
            item = new NotificationDefinition();
            db.Set<NotificationDefinition>().Add(item);
        }
        else
        {
            item = await db.Set<NotificationDefinition>().FirstOrDefaultAsync(n => n.Id == model.Id, cancellationToken);
            if (item is null)
            {
                return Result<Guid>.NotFound("The notification was not found.");
            }
        }

        item.Name = model.Name.Trim();
        item.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        item.TableName = table!.Name;
        item.IsActive = model.IsActive;
        item.OnInsert = model.OnInsert;
        item.OnUpdate = model.OnUpdate;
        item.OnDelete = model.OnDelete;
        item.ConditionJson = ConditionSerializer.Serialize(model.Condition);
        item.TemplateId = model.TemplateId;
        item.Subject = string.IsNullOrWhiteSpace(model.Subject) ? null : model.Subject;
        item.Body = string.IsNullOrWhiteSpace(model.Body) ? null : model.Body;
        item.RecipientFieldsJson = JsonSerializer.Serialize(model.RecipientFields.Select(f => table.FindField(f)!.Name).Distinct().ToList(), s_json);
        item.RecipientGroupIdsJson = JsonSerializer.Serialize(model.RecipientGroupIds.Distinct().ToList(), s_json);
        item.RecipientUserIdsJson = JsonSerializer.Serialize(model.RecipientUserIds.Distinct().ToList(), s_json);
        item.RecipientEmails = NotificationDispatcher.ParseEmails(model.RecipientEmails) is { Count: > 0 } emails ? string.Join(", ", emails) : null;
        item.SendEmail = model.SendEmail;
        item.SendInApp = model.SendInApp;
        item.ExcludeActor = model.ExcludeActor;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("platform", model.Id == Guid.Empty ? "notification.created" : "notification.updated")
        {
            TargetType = "sysevent_email_action",
            TargetId = item.Id.ToString(),
            TargetName = item.Name,
        }, cancellationToken);
        return Result<Guid>.Success(item.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Set<NotificationDefinition>().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("The notification was not found.");
        }

        db.Set<NotificationDefinition>().Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("platform", "notification.deleted") { TargetType = "sysevent_email_action", TargetId = id.ToString(), TargetName = item.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<NotificationTemplateModel>>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<IReadOnlyList<NotificationTemplateModel>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Set<NotificationTemplate>().AsNoTracking().OrderBy(t => t.Name).Take(500).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<NotificationTemplateModel>>.Success(items.Select(t => new NotificationTemplateModel
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            Subject = t.Subject,
            Body = t.Body,
            UpdatedAt = t.UpdatedAt,
        }).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveTemplateAsync(NotificationTemplateModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 150)
        {
            return Result<Guid>.Invalid("The name is required and must not exceed 150 characters.");
        }

        if (string.IsNullOrWhiteSpace(model.Subject) || model.Subject.Length > 500)
        {
            return Result<Guid>.Invalid("The subject is required and must not exceed 500 characters.");
        }

        if (string.IsNullOrWhiteSpace(model.Body) || model.Body.Length > 20_000)
        {
            return Result<Guid>.Invalid("The body is required and must not exceed 20,000 characters.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var name = model.Name.Trim();
        if (await db.Set<NotificationTemplate>().AnyAsync(t => t.Name == name && t.Id != model.Id, cancellationToken))
        {
            return Result<Guid>.Conflict($"A template named '{name}' already exists.");
        }

        NotificationTemplate? item;
        if (model.Id == Guid.Empty)
        {
            item = new NotificationTemplate();
            db.Set<NotificationTemplate>().Add(item);
        }
        else
        {
            item = await db.Set<NotificationTemplate>().FirstOrDefaultAsync(t => t.Id == model.Id, cancellationToken);
            if (item is null)
            {
                return Result<Guid>.NotFound("The template was not found.");
            }
        }

        item.Name = name;
        item.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        item.Subject = model.Subject;
        item.Body = model.Body;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("platform", model.Id == Guid.Empty ? "emailtemplate.created" : "emailtemplate.updated")
        {
            TargetType = "sysevent_email_template",
            TargetId = item.Id.ToString(),
            TargetName = item.Name,
        }, cancellationToken);
        return Result<Guid>.Success(item.Id);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Set<NotificationDefinition>().AnyAsync(n => n.TemplateId == id, cancellationToken))
        {
            return Result.Invalid("The template is used by notifications.");
        }

        var item = await db.Set<NotificationTemplate>().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("The template was not found.");
        }

        db.Set<NotificationTemplate>().Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("platform", "emailtemplate.deleted") { TargetType = "sysevent_email_template", TargetId = id.ToString(), TargetName = item.Name }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<RenderedNotification>> PreviewAsync(NotificationModel model, Guid? recordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<RenderedNotification>.Forbidden();
        }

        var table = await registry.GetTableAsync(model.TableName ?? string.Empty, cancellationToken);
        if (table is null)
        {
            return Result<RenderedNotification>.Invalid("Select an existing table.");
        }

        string? subject = model.Subject;
        string? body = model.Body;
        if (model.TemplateId is { } templateId)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var template = await db.Set<NotificationTemplate>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
            subject = template?.Subject ?? subject;
            body = template?.Body ?? body;
        }

        // Preview with a real record the administrator may read, otherwise with field labels as sample values.
        IReadOnlyDictionary<string, object?>? row = null;
        if (recordId is { } id)
        {
            var single = await records.GetAsync(table.Name, id, cancellationToken);
            row = single.Succeeded ? single.Value : null;
        }
        else
        {
            var page = await records.QueryAsync(table.Name, new RecordQuery { Page = new PageRequest { PageSize = 1 } }, cancellationToken);
            row = page.Succeeded && page.Value!.Items.Count > 0 ? page.Value.Items[0] : null;
        }

        var values = row is not null
            ? new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)
            : table.Fields.ToDictionary(f => f.Name, f => (object?)$"<{f.Label}>", StringComparer.OrdinalIgnoreCase);
        var previewId = ConditionValueConverter.TryGetGuid(values.GetValueOrDefault(nameof(Entity.Id)), out var rid) ? rid : Guid.Empty;
        var placeholders = await renderer.BuildValuesAsync(table, previewId, values, currentUser.DisplayName, "updated", cancellationToken);
        return Result<RenderedNotification>.Success(NotificationRenderer.Render(subject, body, placeholders));
    }

    private static NotificationModel ToModel(NotificationDefinition item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        TableName = item.TableName,
        IsActive = item.IsActive,
        OnInsert = item.OnInsert,
        OnUpdate = item.OnUpdate,
        OnDelete = item.OnDelete,
        Condition = ConditionSerializer.Deserialize(item.ConditionJson),
        TemplateId = item.TemplateId,
        Subject = item.Subject,
        Body = item.Body,
        RecipientFields = NotificationDispatcher.ParseStrings(item.RecipientFieldsJson),
        RecipientGroupIds = NotificationDispatcher.ParseIds(item.RecipientGroupIdsJson),
        RecipientUserIds = NotificationDispatcher.ParseIds(item.RecipientUserIdsJson),
        RecipientEmails = item.RecipientEmails,
        SendEmail = item.SendEmail,
        SendInApp = item.SendInApp,
        ExcludeActor = item.ExcludeActor,
        UpdatedAt = item.UpdatedAt,
    };

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) =>
        currentUser.HasPermissionAsync(Permissions.Platform.ManageNotifications, cancellationToken);

    private static async Task<string?> ValidateAsync(IAppDbContext db, NotificationModel model, TableDefinition? table, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 150)
        {
            return "The name is required and must not exceed 150 characters.";
        }

        if (model.Description is { Length: > 1000 })
        {
            return "The description must not exceed 1000 characters.";
        }

        if (table is null)
        {
            return "Select an existing table.";
        }

        if (ConditionValidator.Validate(table, model.Condition) is { } conditionError)
        {
            return conditionError;
        }

        if (model.TemplateId is { } templateId)
        {
            if (!await db.Set<NotificationTemplate>().AnyAsync(t => t.Id == templateId, cancellationToken))
            {
                return "The selected template does not exist.";
            }
        }
        else if (string.IsNullOrWhiteSpace(model.Subject) || string.IsNullOrWhiteSpace(model.Body))
        {
            return "Enter a subject and a body or select a template.";
        }

        if (model.Subject is { Length: > 500 } || model.Body is { Length: > 20_000 })
        {
            return "The subject must not exceed 500 and the body 20,000 characters.";
        }

        if (!model.SendEmail && !model.SendInApp)
        {
            return "Select e-mail and / or in-app delivery.";
        }

        if (model.RecipientFields.Count > 20 || model.RecipientGroupIds.Count > 50 || model.RecipientUserIds.Count > 200)
        {
            return "Too many recipients (at most 20 fields, 50 groups and 200 users).";
        }

        foreach (var fieldName in model.RecipientFields)
        {
            var field = table.FindField(fieldName);
            if (field is not { Type: FieldType.Reference } || !(string.Equals(field.ReferenceTable, TableNames.User, StringComparison.OrdinalIgnoreCase)
                || string.Equals(field.ReferenceTable, TableNames.Group, StringComparison.OrdinalIgnoreCase)))
            {
                return $"'{fieldName}' is not a reference to users or groups.";
            }
        }

        if (model.RecipientEmails is { Length: > 2000 })
        {
            return "The e-mail address list is too long.";
        }

        var invalidEmail = string.IsNullOrWhiteSpace(model.RecipientEmails)
            ? null
            : model.RecipientEmails.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(e => !NotificationDispatcher.ParseEmails(e).Any());
        if (invalidEmail is not null)
        {
            return $"'{invalidEmail}' is not a valid e-mail address.";
        }

        if (model.RecipientFields.Count == 0 && model.RecipientGroupIds.Count == 0 && model.RecipientUserIds.Count == 0 && string.IsNullOrWhiteSpace(model.RecipientEmails))
        {
            return "Add at least one recipient.";
        }

        return null;
    }
}
