using System.Globalization;
using Servora.Application.Conditions;
using Servora.Application.Tables;

namespace Servora.Web.Components.Platform;

/// <summary>Converts record values into editor strings and human readable display text for generic lists and forms.</summary>
public static class FieldValueFormatter
{
    /// <summary>Converts a record value into the invariant string edited by <c>FieldInput</c>.</summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <returns>The editor text (null for empty values).</returns>
    public static string? ToEditorText(FieldDefinition field, object? value)
    {
        value = ConditionValueConverter.Normalize(value);
        return value switch
        {
            null => null,
            Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            DateTimeOffset d when field.Type == FieldType.Date => d.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            DateTime d => d.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            Guid g => g.ToString("D"),
            _ => ConditionValueConverter.ToText(value),
        };
    }

    /// <summary>Gets the display text of a field of a record row (uses <c>Field.display</c> values when present).</summary>
    /// <param name="field">The field.</param>
    /// <param name="row">The record row.</param>
    /// <returns>The display text (empty for empty values).</returns>
    public static string Display(FieldDefinition field, IReadOnlyDictionary<string, object?> row)
    {
        if (row.TryGetValue(field.Name + ".display", out var display) && display is not null)
        {
            return ConditionValueConverter.ToText(display) ?? string.Empty;
        }

        var value = ConditionValueConverter.Normalize(row.GetValueOrDefault(field.Name));
        return value switch
        {
            null => string.Empty,
            DateTimeOffset d when field.Type == FieldType.Date => d.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC",
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool b => b ? "Yes" : "No",
            Enum e => e.ToString(),
            _ => ConditionValueConverter.ToText(value) ?? string.Empty,
        };
    }

    /// <summary>Gets a safe http(s) URL for a URL field value, or null.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The URL or <see langword="null"/>.</returns>
    public static string? SafeUrl(object? value) =>
        Uri.TryCreate(ConditionValueConverter.ToText(value), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;

    /// <summary>Gets the icon of a field type for the studio.</summary>
    /// <param name="type">The field type.</param>
    /// <returns>The icon name.</returns>
    public static string TypeIcon(FieldType type) => type switch
    {
        FieldType.String => "edit",
        FieldType.Text => "file-text",
        FieldType.Integer or FieldType.Decimal => "bar-chart",
        FieldType.Boolean => "check-square",
        FieldType.DateTime => "clock",
        FieldType.Date => "calendar",
        FieldType.Choice => "list",
        FieldType.Reference => "link",
        FieldType.Guid => "key",
        FieldType.Email => "mail",
        FieldType.Url => "globe",
        FieldType.Json => "braces",
        _ => "dot",
    };

    /// <summary>Gets the human readable name of a field type.</summary>
    /// <param name="type">The field type.</param>
    /// <returns>The name.</returns>
    public static string TypeName(FieldType type) => type switch
    {
        FieldType.String => "Text",
        FieldType.Text => "Multi-line text",
        FieldType.Integer => "Whole number",
        FieldType.Decimal => "Decimal",
        FieldType.Boolean => "True / false",
        FieldType.DateTime => "Date and time",
        FieldType.Date => "Date",
        FieldType.Choice => "Choice",
        FieldType.Reference => "Reference",
        FieldType.Guid => "Identifier",
        FieldType.Email => "E-mail",
        FieldType.Url => "URL",
        FieldType.Json => "JSON",
        _ => type.ToString(),
    };
}
