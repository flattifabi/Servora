using System.Globalization;
using Microsoft.Extensions.Options;
using Servora.Application.Conditions;
using Servora.Application.Platform;
using Servora.Application.Tables;

namespace Servora.Application.Notifications;

/// <summary>
/// Renders notification subject and body for a record: resolves display values of references and choices, adds
/// built-in placeholders (<c>${table}</c>, <c>${link}</c>, <c>${actor}</c>, <c>${event}</c>) and HTML-encodes every value.
/// </summary>
/// <param name="lookup">The record look-up service (display values).</param>
/// <param name="options">The platform options (public URL).</param>
public sealed class NotificationRenderer(IRecordLookupService lookup, IOptions<PlatformOptions> options)
{
    private const int MaxReferenceLookups = 30;

    /// <summary>Builds the placeholder values of a record (exposed fields, display values and built-in placeholders).</summary>
    /// <param name="table">The table.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="values">The exposed field values.</param>
    /// <param name="actorName">The actor that caused the change.</param>
    /// <param name="eventName">The event name (created, updated, deleted).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The placeholder values.</returns>
    public async Task<Dictionary<string, object?>> BuildValuesAsync(TableDefinition table, Guid recordId, IReadOnlyDictionary<string, object?> values, string? actorName, string? eventName, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        var lookups = 0;
        foreach (var field in table.Fields)
        {
            var value = values.GetValueOrDefault(field.Name);
            if (value is null)
            {
                continue;
            }

            if (field.Type == FieldType.Choice && field.Choices.Count > 0)
            {
                var key = value is Enum ? Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : ConditionValueConverter.ToText(value);
                result[field.Name + ".display"] = field.Choices.FirstOrDefault(c => string.Equals(c.Value, key, StringComparison.OrdinalIgnoreCase))?.Label ?? key;
            }
            else if (field.Type == FieldType.Reference && field.ReferenceTable is not null && lookups < MaxReferenceLookups
                     && ConditionValueConverter.TryGetGuid(value, out var id))
            {
                lookups++;
                var item = await lookup.ResolveAsync(field.ReferenceTable, id, cancellationToken);
                if (item is not null)
                {
                    result[field.Name + ".display"] = item.Display;
                }
            }
        }

        var relative = RecordUrls.For(table, recordId);
        result["table"] = table.Label;
        result["link"] = RecordUrls.Absolute(options.Value.PublicUrl, relative);
        result["actor"] = actorName;
        result["event"] = eventName;
        result["sys_id"] = recordId.ToString();
        return result;
    }

    /// <summary>Renders a notification.</summary>
    /// <param name="subjectTemplate">The subject template.</param>
    /// <param name="bodyTemplate">The body template.</param>
    /// <param name="values">The placeholder values (see <see cref="BuildValuesAsync"/>).</param>
    /// <returns>The rendered notification.</returns>
    public static RenderedNotification Render(string? subjectTemplate, string? bodyTemplate, IReadOnlyDictionary<string, object?> values)
    {
        var subject = TemplateRenderer.RenderLine(subjectTemplate, values);
        if (subject.Length == 0)
        {
            subject = "Servora notification";
        }

        var link = TemplateRenderer.Format(values.GetValueOrDefault("link"));
        var html = EmailLayout.Wrap(subject, TemplateRenderer.RenderHtml(bodyTemplate, values), link);
        return new RenderedNotification(subject, html, TemplateRenderer.RenderText(bodyTemplate, values), link);
    }
}
