using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Servora.Application.Automation.Templates;

/// <summary>
/// Safe data pill template resolver. Replaces <c>{{ path }}</c> expressions with values from a JSON data tree. There
/// is no code execution: an expression is a dotted path (identifiers and numeric indexes) followed by optional
/// whitelisted filters (<c>upper</c>, <c>lower</c>, <c>trim</c>, <c>json</c>, <c>urlencode</c>, <c>default:'text'</c>).
/// Resolution is single-pass, so values that themselves contain <c>{{…}}</c> are never evaluated again.
/// </summary>
public static partial class FlowTemplate
{
    /// <summary>The maximum template length that is processed.</summary>
    public const int MaxTemplateLength = 100_000;

    /// <summary>The maximum rendered output length.</summary>
    public const int MaxOutputLength = 200_000;

    private const int MaxPathSegments = 16;

    /// <summary>Renders a template to text. Unknown paths render as empty text.</summary>
    /// <param name="template">The template.</param>
    /// <param name="data">The data tree (for flows: <c>trigger</c>, <c>inputs</c>, <c>vars</c>, <c>steps</c>, <c>run</c>, <c>flow</c>).</param>
    /// <returns>The rendered text.</returns>
    public static string Render(string? template, JsonNode? data)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        if (template.Length > MaxTemplateLength)
        {
            template = template[..MaxTemplateLength];
        }

        if (!template.Contains("{{", StringComparison.Ordinal))
        {
            return template;
        }

        string result;
        try
        {
            result = ExpressionRegex().Replace(template, match => ToText(Evaluate(match.Groups["path"].Value, match.Groups["filters"].Value, data)));
        }
        catch (RegexMatchTimeoutException)
        {
            return template;
        }

        return result.Length > MaxOutputLength ? result[..MaxOutputLength] : result;
    }

    /// <summary>
    /// Resolves a template to a typed value: when the whole template is a single expression the referenced node is
    /// returned as-is (numbers stay numbers, objects stay objects); otherwise the rendered text is returned.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="data">The data tree.</param>
    /// <returns>The value or <see langword="null"/>.</returns>
    public static JsonNode? ResolveValue(string? template, JsonNode? data)
    {
        if (template is null)
        {
            return null;
        }

        var trimmed = template.Trim();
        if (trimmed.Length <= MaxTemplateLength)
        {
            try
            {
                var match = ExpressionRegex().Match(trimmed);
                if (match.Success && match.Index == 0 && match.Length == trimmed.Length)
                {
                    return Evaluate(match.Groups["path"].Value, match.Groups["filters"].Value, data)?.DeepClone();
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return JsonValue.Create(template);
            }
        }

        return JsonValue.Create(Render(template, data));
    }

    /// <summary>Looks up a dotted path (<c>a.b[0].c</c> or <c>a.b.0.c</c>; <c>count</c> on arrays) in a data tree.</summary>
    /// <param name="data">The data tree.</param>
    /// <param name="path">The path.</param>
    /// <returns>The node or <see langword="null"/> when the path does not exist or is invalid.</returns>
    public static JsonNode? Lookup(JsonNode? data, string? path)
    {
        if (data is null || string.IsNullOrWhiteSpace(path))
        {
            return string.IsNullOrWhiteSpace(path) ? data : null;
        }

        var segments = SplitPath(path);
        if (segments is null)
        {
            return null;
        }

        var current = data;
        foreach (var segment in segments)
        {
            if (current is null)
            {
                return null;
            }

            current = Step(current, segment);
        }

        return current;
    }

    /// <summary>Extracts the paths of all expressions of a template (for validation and pill highlighting).</summary>
    /// <param name="template">The template.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> ExtractPaths(string? template)
    {
        if (string.IsNullOrEmpty(template) || template.Length > MaxTemplateLength)
        {
            return [];
        }

        try
        {
            return ExpressionRegex().Matches(template).Select(m => m.Groups["path"].Value.Trim()).ToList();
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    /// <summary>Converts a node to its display text (strings unquoted, numbers invariant, objects as compact JSON).</summary>
    /// <param name="node">The node.</param>
    /// <returns>The text.</returns>
    public static string ToText(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return string.Empty;
            case JsonValue value:
                return value.GetValueKind() switch
                {
                    JsonValueKind.String => value.TryGetValue<string>(out var text) ? text : value.ToJsonString().Trim('"'),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => value.ToJsonString(),
                };
            default:
                return node.ToJsonString();
        }
    }

    private static JsonNode? Evaluate(string path, string filters, JsonNode? data)
    {
        var node = Lookup(data, path.Trim());
        if (string.IsNullOrWhiteSpace(filters))
        {
            return node;
        }

        foreach (var raw in filters.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            node = ApplyFilter(node, raw);
        }

        return node;
    }

    private static JsonNode? ApplyFilter(JsonNode? node, string filter)
    {
        var name = filter;
        string? argument = null;
        var colon = filter.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            name = filter[..colon].Trim();
            argument = filter[(colon + 1)..].Trim().Trim('\'', '"');
        }

        return name.ToLowerInvariant() switch
        {
            "upper" => JsonValue.Create(ToText(node).ToUpperInvariant()),
            "lower" => JsonValue.Create(ToText(node).ToLowerInvariant()),
            "trim" => JsonValue.Create(ToText(node).Trim()),
            "json" => JsonValue.Create(node?.ToJsonString() ?? "null"),
            "urlencode" => JsonValue.Create(Uri.EscapeDataString(ToText(node))),
            "default" => string.IsNullOrEmpty(ToText(node)) ? JsonValue.Create(argument ?? string.Empty) : node,
            _ => node,
        };
    }

    private static List<string>? SplitPath(string path)
    {
        var segments = new List<string>();
        var builder = new StringBuilder();
        foreach (var ch in path.Trim())
        {
            switch (ch)
            {
                case '.':
                case '[':
                case ']':
                    if (builder.Length > 0)
                    {
                        segments.Add(builder.ToString());
                        builder.Clear();
                    }

                    break;
                default:
                    if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-')
                    {
                        return null;
                    }

                    builder.Append(ch);
                    break;
            }
        }

        if (builder.Length > 0)
        {
            segments.Add(builder.ToString());
        }

        // Private segments (internal engine state) are never addressable.
        return segments.Count is 0 or > MaxPathSegments || segments.Any(s => s.StartsWith('_')) ? null : segments;
    }

    private static JsonNode? Step(JsonNode current, string segment)
    {
        switch (current)
        {
            case JsonObject obj:
                if (obj.TryGetPropertyValue(segment, out var exact))
                {
                    return exact;
                }

                foreach (var pair in obj)
                {
                    if (string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }

                return null;
            case JsonArray array:
                if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    return index >= 0 && index < array.Count ? array[index] : null;
                }

                if (string.Equals(segment, "count", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "length", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonValue.Create(array.Count);
                }

                if (string.Equals(segment, "first", StringComparison.OrdinalIgnoreCase))
                {
                    return array.Count > 0 ? array[0] : null;
                }

                return null;
            default:
                return null;
        }
    }

    [GeneratedRegex(@"\{\{\s*(?<path>[^{}|]+?)\s*(?<filters>(\|[^{}|]+)*)\}\}", RegexOptions.CultureInvariant, 250)]
    private static partial Regex ExpressionRegex();
}
