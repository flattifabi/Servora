using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Servora.Application.Mcp;

/// <summary>
/// Minimal, dependency-free JSON Schema validator for tool inputs. Supported keywords: <c>type</c> (single or array,
/// including <c>null</c>), <c>properties</c>, <c>required</c>, <c>additionalProperties: false</c>, <c>enum</c>,
/// <c>minLength</c>, <c>maxLength</c>, <c>pattern</c>, <c>format</c> (<c>uuid</c>, <c>date-time</c>), <c>minimum</c>,
/// <c>maximum</c>, <c>items</c>, <c>minItems</c>, <c>maxItems</c>, <c>maxProperties</c>.
/// </summary>
public static class JsonSchemaValidator
{
    private const int MaxErrors = 20;
    private static readonly TimeSpan s_regexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Validates a value against a schema.</summary>
    /// <param name="value">The value.</param>
    /// <param name="schema">The schema.</param>
    /// <returns>The validation errors (empty when valid).</returns>
    public static IReadOnlyList<string> Validate(JsonNode? value, JsonObject schema)
    {
        var errors = new List<string>();
        ValidateNode(value, schema, "$", errors);
        return errors.Count > MaxErrors ? errors.GetRange(0, MaxErrors) : errors;
    }

    private static void ValidateNode(JsonNode? value, JsonObject schema, string path, List<string> errors)
    {
        if (errors.Count >= MaxErrors)
        {
            return;
        }

        var kind = value?.GetValueKind() ?? JsonValueKind.Null;
        if (schema["type"] is { } typeNode)
        {
            var allowed = typeNode is JsonArray array
                ? array.Select(t => t?.GetValue<string>()).OfType<string>().ToList()
                : [typeNode.GetValue<string>()];
            if (!allowed.Any(t => Matches(t, kind, value)))
            {
                errors.Add($"{path}: expected {string.Join(" or ", allowed)} but got {Describe(kind)}.");
                return;
            }
        }

        if (schema["enum"] is JsonArray options && !options.Any(o => JsonNode.DeepEquals(o, value)))
        {
            errors.Add($"{path}: must be one of {string.Join(", ", options.Select(o => o?.ToJsonString() ?? "null"))}.");
        }

        switch (kind)
        {
            case JsonValueKind.String:
                ValidateString(value!.GetValue<string>(), schema, path, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(AsDouble(value!), schema, path, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray((JsonArray)value!, schema, path, errors);
                break;
            case JsonValueKind.Object:
                ValidateObject((JsonObject)value!, schema, path, errors);
                break;
        }
    }

    private static void ValidateString(string text, JsonObject schema, string path, List<string> errors)
    {
        if (schema["minLength"] is JsonValue min && text.Length < min.GetValue<int>())
        {
            errors.Add($"{path}: must have at least {min.GetValue<int>()} characters.");
        }

        if (schema["maxLength"] is JsonValue max && text.Length > max.GetValue<int>())
        {
            errors.Add($"{path}: must have at most {max.GetValue<int>()} characters.");
        }

        if (schema["pattern"] is JsonValue pattern)
        {
            try
            {
                if (!Regex.IsMatch(text, pattern.GetValue<string>(), RegexOptions.CultureInvariant, s_regexTimeout))
                {
                    errors.Add($"{path}: does not match the required pattern.");
                }
            }
            catch (RegexMatchTimeoutException)
            {
                errors.Add($"{path}: could not be validated.");
            }
        }

        switch (schema["format"]?.GetValue<string>())
        {
            case "uuid" when !Guid.TryParse(text, out _):
                errors.Add($"{path}: must be a UUID.");
                break;
            case "date-time" when !DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out _):
                errors.Add($"{path}: must be an ISO 8601 date-time.");
                break;
        }
    }

    private static void ValidateNumber(double number, JsonObject schema, string path, List<string> errors)
    {
        if (schema["minimum"] is JsonValue min && number < min.GetValue<double>())
        {
            errors.Add($"{path}: must be >= {min.ToJsonString()}.");
        }

        if (schema["maximum"] is JsonValue max && number > max.GetValue<double>())
        {
            errors.Add($"{path}: must be <= {max.ToJsonString()}.");
        }
    }

    private static void ValidateArray(JsonArray array, JsonObject schema, string path, List<string> errors)
    {
        if (schema["minItems"] is JsonValue min && array.Count < min.GetValue<int>())
        {
            errors.Add($"{path}: must contain at least {min.GetValue<int>()} items.");
        }

        if (schema["maxItems"] is JsonValue max && array.Count > max.GetValue<int>())
        {
            errors.Add($"{path}: must contain at most {max.GetValue<int>()} items.");
            return;
        }

        if (schema["items"] is JsonObject itemSchema)
        {
            for (var i = 0; i < array.Count; i++)
            {
                ValidateNode(array[i], itemSchema, $"{path}[{i}]", errors);
            }
        }
    }

    private static void ValidateObject(JsonObject value, JsonObject schema, string path, List<string> errors)
    {
        if (schema["maxProperties"] is JsonValue maxProperties && value.Count > maxProperties.GetValue<int>())
        {
            errors.Add($"{path}: must have at most {maxProperties.GetValue<int>()} properties.");
            return;
        }

        var properties = schema["properties"] as JsonObject;
        if (schema["required"] is JsonArray required)
        {
            foreach (var name in required.Select(r => r?.GetValue<string>()).OfType<string>())
            {
                if (!value.ContainsKey(name) || value[name] is null)
                {
                    errors.Add($"{path}.{name}: is required.");
                }
            }
        }

        var additional = schema["additionalProperties"];
        foreach (var (name, child) in value)
        {
            if (errors.Count >= MaxErrors)
            {
                return;
            }

            if (properties?[name] is JsonObject propertySchema)
            {
                ValidateNode(child, propertySchema, $"{path}.{name}", errors);
            }
            else if (additional is JsonValue allow && allow.GetValueKind() == JsonValueKind.False)
            {
                errors.Add($"{path}.{name}: is not an allowed property.");
            }
            else if (additional is JsonObject additionalSchema)
            {
                ValidateNode(child, additionalSchema, $"{path}.{name}", errors);
            }
        }
    }

    private static bool Matches(string type, JsonValueKind kind, JsonNode? value) => type switch
    {
        "null" => kind == JsonValueKind.Null,
        "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
        "string" => kind == JsonValueKind.String,
        "number" => kind == JsonValueKind.Number,
        "integer" => kind == JsonValueKind.Number && Math.Abs(AsDouble(value!) % 1) < double.Epsilon,
        "array" => kind == JsonValueKind.Array,
        "object" => kind == JsonValueKind.Object,
        _ => true,
    };

    private static double AsDouble(JsonNode node) =>
        double.TryParse(node.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : double.NaN;

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Undefined => "nothing",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
