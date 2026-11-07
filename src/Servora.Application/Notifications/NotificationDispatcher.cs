using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Application.Platform;
using Servora.Application.Tables;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Notifications;

namespace Servora.Application.Notifications;

/// <summary>
/// Default <see cref="INotificationDispatcher"/>. Recipients are resolved from reference fields (users and group
/// members), fixed groups, fixed users and fixed addresses; each address receives its own message so recipients never
/// see each other. Recipient counts are bounded.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="registry">The table registry.</param>
/// <param name="evaluator">The condition evaluator.</param>
/// <param name="renderer">The notification renderer.</param>
/// <param name="emailSender">The e-mail sender.</param>
/// <param name="activity">The activity service.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class NotificationDispatcher(
    IAppDbContextFactory dbFactory,
    ITableRegistry registry,
    IConditionEvaluator evaluator,
    NotificationRenderer renderer,
    IEmailSender emailSender,
    IActivityService activity,
    IClock clock,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    /// <summary>The maximum number of recipients per notification.</summary>
    public const int MaxRecipients = 200;

    private const int MaxDefinitionsPerEvent = 100;
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<int> DispatchAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken = default)
    {
        List<NotificationDefinition> definitions;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var query = db.Set<NotificationDefinition>().AsNoTracking().Where(n => n.TableName == recordEvent.TableName && n.IsActive);
            query = recordEvent.ChangeType switch
            {
                RecordChangeType.Created => query.Where(n => n.OnInsert),
                RecordChangeType.Updated => query.Where(n => n.OnUpdate),
                _ => query.Where(n => n.OnDelete),
            };
            definitions = await query.OrderBy(n => n.Name).Take(MaxDefinitionsPerEvent).ToListAsync(cancellationToken);
        }

        if (definitions.Count == 0)
        {
            return 0;
        }

        var table = await registry.GetTableAsync(recordEvent.TableName, cancellationToken);
        if (table is null)
        {
            return 0;
        }

        var values = RecordEntityAccessor.FilterExposed(table, recordEvent.Snapshot);
        var context = new ConditionContext(recordEvent.ActorUserId, RecordChangeExpander.Expand(recordEvent.Changes));
        var sent = 0;
        foreach (var definition in definitions)
        {
            if (!evaluator.Evaluate(ConditionSerializer.Deserialize(definition.ConditionJson), values, context))
            {
                continue;
            }

            try
            {
                await DeliverAsync(definition, table, recordEvent, values, cancellationToken);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification {Notification} failed for {Table}/{RecordId}.", definition.Name, recordEvent.TableName, recordEvent.RecordId);
            }
        }

        return sent;
    }

    /// <inheritdoc />
    public async Task<int> SendAsync(Guid notificationId, RecordChangedEvent recordEvent, CancellationToken cancellationToken = default)
    {
        NotificationDefinition? definition;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            definition = await db.Set<NotificationDefinition>().AsNoTracking().FirstOrDefaultAsync(n => n.Id == notificationId && n.IsActive, cancellationToken);
        }

        if (definition is null || !string.Equals(definition.TableName, recordEvent.TableName, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var table = await registry.GetTableAsync(recordEvent.TableName, cancellationToken);
        if (table is null)
        {
            return 0;
        }

        return await DeliverAsync(definition, table, recordEvent, RecordEntityAccessor.FilterExposed(table, recordEvent.Snapshot), cancellationToken);
    }

    /// <summary>Parses a JSON array of identifiers.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The identifiers.</returns>
    public static List<Guid> ParseIds(string? json) => ParseList<Guid>(json);

    /// <summary>Parses a JSON array of strings.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The strings.</returns>
    public static List<string> ParseStrings(string? json) => ParseList<string>(json);

    /// <summary>Parses a comma or semicolon separated list of e-mail addresses, keeping only valid addresses.</summary>
    /// <param name="text">The list.</param>
    /// <returns>The valid addresses (at most 20).</returns>
    public static List<string> ParseEmails(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => e.Length <= 254 && MailAddress.TryCreate(e, out var a) && string.Equals(a.Address, e, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

    private static List<T> ParseList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, s_json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<int> DeliverAsync(NotificationDefinition definition, TableDefinition table, RecordChangedEvent recordEvent, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    {
        string? subjectTemplate = definition.Subject;
        string? bodyTemplate = definition.Body;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (definition.TemplateId is { } templateId)
        {
            var template = await db.Set<NotificationTemplate>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
            if (template is not null)
            {
                subjectTemplate = template.Subject;
                bodyTemplate = template.Body;
            }
        }

        var placeholders = await renderer.BuildValuesAsync(table, recordEvent.RecordId, values, recordEvent.ActorName, recordEvent.ChangeType.ToString().ToLowerInvariant(), cancellationToken);
        var message = NotificationRenderer.Render(subjectTemplate, bodyTemplate, placeholders);

        var (userIds, emails) = await ResolveRecipientsAsync(db, definition, table, values, cancellationToken);
        if (definition.ExcludeActor && recordEvent.ActorUserId is { } actorId)
        {
            userIds.Remove(actorId);
        }

        var userEmails = userIds.Count == 0
            ? []
            : await db.Set<User>().AsNoTracking()
                .Where(u => userIds.Contains(u.Id) && u.IsActive && u.Email != null && u.Email != string.Empty)
                .Select(u => new { u.Id, u.Email })
                .Take(MaxRecipients)
                .ToListAsync(cancellationToken);
        var activeUserIds = definition.SendInApp && userIds.Count > 0
            ? await db.Set<User>().AsNoTracking().Where(u => userIds.Contains(u.Id) && u.IsActive).Select(u => u.Id).Take(MaxRecipients).ToListAsync(cancellationToken)
            : [];

        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in userEmails.Select(u => u.Email!).Concat(emails))
        {
            if (addresses.Count >= MaxRecipients)
            {
                break;
            }

            addresses.Add(email);
        }

        var delivered = 0;
        if (definition.SendEmail)
        {
            foreach (var address in addresses)
            {
                if (await emailSender.SendAsync(new EmailMessage([address], message.Subject, message.HtmlBody) { TextBody = message.TextBody }, cancellationToken))
                {
                    delivered++;
                }
            }
        }

        if (definition.SendInApp && activeUserIds.Count > 0)
        {
            var now = clock.UtcNow;
            var body = message.TextBody.Length > 2000 ? message.TextBody[..2000] : message.TextBody;
            var link = recordEvent.ChangeType == RecordChangeType.Deleted ? null : RecordUrls.For(table, recordEvent.RecordId);
            foreach (var userId in activeUserIds)
            {
                db.Set<InAppNotification>().Add(new InAppNotification
                {
                    UserId = userId,
                    Title = message.Subject,
                    Body = body,
                    Link = link,
                    TableName = table.Name,
                    RecordId = recordEvent.RecordId,
                    CreatedAt = now,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var total = delivered + activeUserIds.Count;
        if (recordEvent.ChangeType != RecordChangeType.Deleted)
        {
            await activity.AddSystemAsync(table.Name, recordEvent.RecordId,
                $"Notification '{definition.Name}' processed: {delivered} e-mail(s) sent of {(definition.SendEmail ? addresses.Count : 0)}, {activeUserIds.Count} in-app message(s).",
                cancellationToken);
        }

        return total;
    }

    private static async Task<(HashSet<Guid> UserIds, List<string> Emails)> ResolveRecipientsAsync(IAppDbContext db, NotificationDefinition definition, TableDefinition table, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>(ParseIds(definition.RecipientUserIdsJson).Take(MaxRecipients));
        var groupIds = new HashSet<Guid>(ParseIds(definition.RecipientGroupIdsJson).Take(50));
        foreach (var fieldName in ParseStrings(definition.RecipientFieldsJson).Take(20))
        {
            var field = table.FindField(fieldName);
            if (field is not { Type: FieldType.Reference } || !ConditionValueConverter.TryGetGuid(values.GetValueOrDefault(field.Name), out var id))
            {
                continue;
            }

            if (string.Equals(field.ReferenceTable, TableNames.User, StringComparison.OrdinalIgnoreCase))
            {
                userIds.Add(id);
            }
            else if (string.Equals(field.ReferenceTable, TableNames.Group, StringComparison.OrdinalIgnoreCase))
            {
                groupIds.Add(id);
            }
        }

        if (groupIds.Count > 0)
        {
            var members = await db.Set<GroupMember>().AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId))
                .Select(m => m.UserId)
                .Take(MaxRecipients)
                .ToListAsync(cancellationToken);
            foreach (var member in members)
            {
                userIds.Add(member);
            }
        }

        return (userIds, ParseEmails(definition.RecipientEmails));
    }
}
