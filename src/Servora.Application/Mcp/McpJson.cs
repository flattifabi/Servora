using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Servora.Application.Mcp;

/// <summary>JSON helpers shared by MCP tools, the MCP server and the AI orchestrator.</summary>
public static class McpJson
{
    /// <summary>
    /// Gets the serializer options (camelCase, string enums, bounded depth). Relaxed escaping keeps tool output compact and
    /// readable for models; the text is always re-encoded by the JSON-RPC envelope and HTML-encoded by the UI.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 32,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Gets the document options used to parse untrusted JSON.</summary>
    public static JsonDocumentOptions DocumentOptions { get; } = new() { MaxDepth = McpLimits.MaxJsonDepth };

    /// <summary>Serializes a value to a JSON node.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The node (an empty object for <see langword="null"/>).</returns>
    public static JsonNode ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, Options) ?? new JsonObject();

    /// <summary>Parses untrusted JSON safely.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="node">The parsed node.</param>
    /// <returns><see langword="true"/> when the text is valid JSON.</returns>
    public static bool TryParse(string? json, out JsonNode? node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            node = JsonNode.Parse(json, documentOptions: DocumentOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Serializes a node to text, truncated to <paramref name="maxChars"/>.</summary>
    /// <param name="node">The node.</param>
    /// <param name="maxChars">The maximum length.</param>
    /// <param name="truncated">Set when the text was truncated.</param>
    /// <returns>The text.</returns>
    public static string ToBoundedText(JsonNode? node, int maxChars, out bool truncated)
    {
        var text = node?.ToJsonString(Options) ?? "null";
        truncated = text.Length > maxChars;
        return truncated ? text[..maxChars] + "\n…[truncated: result exceeded the output limit; narrow the query]" : text;
    }

    /// <summary>Truncates a string.</summary>
    /// <param name="value">The value.</param>
    /// <param name="maxChars">The maximum length.</param>
    /// <returns>The truncated value.</returns>
    public static string Truncate(string value, int maxChars) => value.Length <= maxChars ? value : value[..maxChars] + "…";

    /// <summary>Reads a string argument.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The trimmed value or <see langword="null"/>.</returns>
    public static string? GetString(JsonObject arguments, string name) =>
        arguments[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    /// <summary>Reads an integer argument.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <param name="fallback">The default value.</param>
    /// <returns>The value.</returns>
    public static int GetInt(JsonObject arguments, string name, int fallback) =>
        arguments[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number
            && double.TryParse(value.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? (int)Math.Clamp(number, int.MinValue, int.MaxValue)
            : fallback;

    /// <summary>Reads a GUID argument.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The value or <see langword="null"/>.</returns>
    public static Guid? GetGuid(JsonObject arguments, string name) => Guid.TryParse(GetString(arguments, name), out var id) ? id : null;

    /// <summary>Reads a string array argument.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The values (empty when missing).</returns>
    public static IReadOnlyList<string> GetStringArray(JsonObject arguments, string name) =>
        arguments[name] is JsonArray array
            ? array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToList()
            : [];

    /// <summary>Reads an object argument as a string dictionary (values converted to invariant strings).</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The dictionary (empty when missing).</returns>
    public static IReadOnlyDictionary<string, string> GetStringMap(JsonObject arguments, string name)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (arguments[name] is JsonObject map)
        {
            foreach (var (key, value) in map)
            {
                if (value is JsonValue v)
                {
                    result[key] = v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString();
                }
            }
        }

        return result;
    }
}
