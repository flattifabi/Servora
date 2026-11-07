using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Automation.Templates;

namespace Servora.Application.Automation.Actions;

/// <summary>Conversions between resolved data pill values and the value shapes expected by platform services.</summary>
public static class FlowValues
{
    /// <summary>Converts a resolved node into a record field value (string, number, boolean or JSON element).</summary>
    /// <param name="node">The node.</param>
    /// <returns>The value.</returns>
    public static object? ToRecordValue(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue value:
                switch (value.GetValueKind())
                {
                    case JsonValueKind.String:
                        return FlowTemplate.ToText(value);
                    case JsonValueKind.True:
                        return true;
                    case JsonValueKind.False:
                        return false;
                    case JsonValueKind.Number:
                        var text = value.ToJsonString();
                        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                        {
                            return integer;
                        }

                        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : text;
                    default:
                        return null;
                }

            default:
                using (var document = JsonDocument.Parse(node.ToJsonString()))
                {
                    return document.RootElement.Clone();
                }
        }
    }

    /// <summary>Converts a record value dictionary into a JSON object (for outputs).</summary>
    /// <param name="record">The record values.</param>
    /// <returns>The object.</returns>
    public static JsonObject ToJsonObject(IReadOnlyDictionary<string, object?>? record)
    {
        var obj = new JsonObject();
        if (record is null)
        {
            return obj;
        }

        foreach (var pair in record.Take(500))
        {
            try
            {
                obj[pair.Key] = pair.Value switch
                {
                    null => null,
                    JsonElement element => JsonNode.Parse(element.GetRawText()),
                    _ => JsonSerializer.SerializeToNode(pair.Value, pair.Value.GetType()),
                };
            }
            catch (NotSupportedException)
            {
                obj[pair.Key] = Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
            }
        }

        return obj;
    }

    /// <summary>Parses a comma / semicolon / whitespace separated list of identifiers.</summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The maximum number of identifiers.</param>
    /// <returns>The distinct valid identifiers.</returns>
    public static IReadOnlyList<Guid> ParseGuids(string? text, int max = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s.Trim('"', '[', ']'), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(max)
            .ToList();
    }

    /// <summary>Parses a single identifier from text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The identifier or <see langword="null"/>.</returns>
    public static Guid? ParseGuid(string? text) => Guid.TryParse(text?.Trim(), out var id) ? id : null;
}
