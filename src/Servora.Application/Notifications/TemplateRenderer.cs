using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Servora.Application.Conditions;

namespace Servora.Application.Notifications;

/// <summary>
/// Replaces <c>${Field}</c> placeholders in plain-text templates. Display values (<c>Field.display</c>) are preferred
/// over raw values. No expressions are evaluated – placeholders are simple, bounded look-ups.
/// </summary>
public static partial class TemplateRenderer
{
    /// <summary>The maximum length of a rendered template.</summary>
    public const int MaxLength = 50_000;

    /// <summary>Renders a template as plain text.</summary>
    /// <param name="template">The template.</param>
    /// <param name="values">The record values (case-insensitive keys, may include <c>Field.display</c> entries).</param>
    /// <returns>The rendered text.</returns>
    public static string RenderText(string? template, IReadOnlyDictionary<string, object?> values) =>
        Render(template, values, encode: false);

    /// <summary>Renders a single-line text (subject): placeholders replaced, control characters and line breaks removed.</summary>
    /// <param name="template">The template.</param>
    /// <param name="values">The record values.</param>
    /// <returns>The rendered single-line text (at most 300 characters).</returns>
    public static string RenderLine(string? template, IReadOnlyDictionary<string, object?> values)
    {
        var text = Render(template, values, encode: false);
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(char.IsControl(c) ? ' ' : c);
        }

        var line = builder.ToString().Trim();
        return line.Length > 300 ? line[..300] : line;
    }

    /// <summary>Renders a template as HTML: the template text and all values are HTML-encoded, line breaks become <c>&lt;br&gt;</c>.</summary>
    /// <param name="template">The template.</param>
    /// <param name="values">The record values.</param>
    /// <returns>The HTML fragment.</returns>
    public static string RenderHtml(string? template, IReadOnlyDictionary<string, object?> values) =>
        Render(template, values, encode: true).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br>\n", StringComparison.Ordinal);

    /// <summary>Formats a value for display in templates.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Format(object? value) => ConditionValueConverter.Normalize(value) switch
    {
        null => string.Empty,
        DateTimeOffset d => d.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        var other => ConditionValueConverter.ToText(other) ?? string.Empty,
    };

    private static string Render(string? template, IReadOnlyDictionary<string, object?> values, bool encode)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        if (template.Length > MaxLength)
        {
            template = template[..MaxLength];
        }

        var lookup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            lookup[key] = value;
        }

        var builder = new StringBuilder(template.Length + 64);
        var position = 0;
        foreach (Match match in PlaceholderPattern().Matches(template))
        {
            builder.Append(encode ? WebUtility.HtmlEncode(template[position..match.Index]) : template[position..match.Index]);
            var name = match.Groups["name"].Value;
            var value = lookup.TryGetValue(name + ".display", out var display) && display is not null
                ? display
                : lookup.GetValueOrDefault(name);
            var text = Format(value);
            builder.Append(encode ? WebUtility.HtmlEncode(text) : text);
            position = match.Index + match.Length;
        }

        builder.Append(encode ? WebUtility.HtmlEncode(template[position..]) : template[position..]);
        var result = builder.ToString();
        return result.Length > MaxLength ? result[..MaxLength] : result;
    }

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]{0,63}(\.display)?)\}")]
    private static partial Regex PlaceholderPattern();
}
