using System.Text.Json.Nodes;

namespace Servora.Application.Automation.Engine;

/// <summary>
/// Masks secret-looking values (passwords, tokens, keys, authorization headers, …) and bounds the size of data that
/// is stored in flow contexts and run step snapshots.
/// </summary>
public static class FlowRedactor
{
    /// <summary>The replacement for masked values.</summary>
    public const string Mask = "***";

    /// <summary>The maximum length of a single string value in stored data.</summary>
    public const int MaxStringLength = 8000;

    private static readonly string[] s_sensitiveFragments =
    [
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "api-key", "authorization",
        "credential", "privatekey", "private_key", "cookie", "sessionid", "session_id", "securitystamp", "connectionstring",
    ];

    /// <summary>Checks whether a property name denotes a sensitive value.</summary>
    /// <param name="name">The property name.</param>
    /// <returns><see langword="true"/> when the value must be masked.</returns>
    public static bool IsSensitiveName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var normalized = name.ToLowerInvariant();
        return s_sensitiveFragments.Any(normalized.Contains);
    }

    /// <summary>Returns a redacted deep copy of a node: sensitive properties are masked and long strings truncated.</summary>
    /// <param name="node">The node.</param>
    /// <param name="depth">The current depth (recursion guard).</param>
    /// <returns>The redacted copy.</returns>
    public static JsonNode? Redact(JsonNode? node, int depth = 0)
    {
        if (node is null)
        {
            return null;
        }

        if (depth > 32)
        {
            return JsonValue.Create("[nested too deeply]");
        }

        switch (node)
        {
            case JsonObject obj:
                var copy = new JsonObject();
                foreach (var pair in obj)
                {
                    copy[pair.Key] = IsSensitiveName(pair.Key) && pair.Value is not null
                        ? JsonValue.Create(Mask)
                        : Redact(pair.Value, depth + 1);
                }

                return copy;
            case JsonArray array:
                var list = new JsonArray();
                foreach (var item in array.Take(1000))
                {
                    list.Add(Redact(item, depth + 1));
                }

                return list;
            default:
                if (node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > MaxStringLength)
                {
                    return JsonValue.Create(text[..MaxStringLength] + "…");
                }

                return node.DeepClone();
        }
    }

    /// <summary>Serializes a node for storage, redacting it and truncating the result to a maximum length.</summary>
    /// <param name="node">The node.</param>
    /// <param name="maxLength">The maximum JSON length.</param>
    /// <returns>The JSON text or <see langword="null"/>.</returns>
    public static string? ToStoredJson(JsonNode? node, int maxLength)
    {
        if (node is null)
        {
            return null;
        }

        var json = Redact(node)!.ToJsonString();
        if (json.Length <= maxLength)
        {
            return json;
        }

        var preview = json[..Math.Max(0, maxLength - 200)];
        return new JsonObject { ["_truncated"] = true, ["preview"] = preview }.ToJsonString();
    }

    /// <summary>Truncates a user-facing message.</summary>
    /// <param name="message">The message.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns>The truncated message.</returns>
    public static string? Truncate(string? message, int maxLength) =>
        message is null || message.Length <= maxLength ? message : message[..(maxLength - 1)] + "…";
}
