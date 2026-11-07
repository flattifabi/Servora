using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>
/// Parses, validates and serializes class attribute definitions (<see cref="CiClass.AttributesJson"/>) and the matching
/// attribute values stored in <see cref="ConfigurationItem.CustomFieldsJson"/>.
/// </summary>
public static partial class CiAttributeSchema
{
    /// <summary>The maximum number of attributes per class.</summary>
    public const int MaxAttributes = 50;

    private const int MaxValueLength = 4000;

    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    /// <summary>Parses attribute definitions; invalid JSON yields an empty list.</summary>
    /// <param name="json">The JSON array.</param>
    /// <returns>The definitions.</returns>
    public static List<CiAttributeDefinition> ParseDefinitions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CiAttributeDefinition>>(json, s_options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Serializes attribute definitions.</summary>
    /// <param name="definitions">The definitions.</param>
    /// <returns>The JSON array, or <see langword="null"/> when empty.</returns>
    public static string? SerializeDefinitions(IReadOnlyList<CiAttributeDefinition> definitions) =>
        definitions.Count == 0 ? null : JsonSerializer.Serialize(definitions, s_options);

    /// <summary>Validates attribute definitions.</summary>
    /// <param name="definitions">The definitions.</param>
    /// <returns>An error message, or <see langword="null"/> when valid.</returns>
    public static string? ValidateDefinitions(IReadOnlyList<CiAttributeDefinition> definitions)
    {
        if (definitions.Count > MaxAttributes)
        {
            return $"A class can define at most {MaxAttributes} attributes.";
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!NameRegex().IsMatch(definition.Name ?? string.Empty))
            {
                return $"Attribute name '{definition.Name}' is invalid. Use lower case letters, digits and underscores, starting with a letter.";
            }

            if (!names.Add(definition.Name!))
            {
                return $"Attribute name '{definition.Name}' is used more than once.";
            }

            if (string.IsNullOrWhiteSpace(definition.Label) || definition.Label.Length > 100)
            {
                return $"Attribute '{definition.Name}' needs a label (max 100 characters).";
            }

            if (!Enum.IsDefined(definition.Type))
            {
                return $"Attribute '{definition.Name}' has an unknown type.";
            }

            if (definition.Type == CiAttributeType.Choice
                && (definition.Choices.Count == 0 || definition.Choices.Any(string.IsNullOrWhiteSpace) || definition.Choices.Count > 100))
            {
                return $"Choice attribute '{definition.Name}' needs between 1 and 100 non-empty choices.";
            }

            if (definition.Help?.Length > 300 || definition.Unit?.Length > 20)
            {
                return $"Attribute '{definition.Name}': help text max 300, unit max 20 characters.";
            }
        }

        return null;
    }

    /// <summary>Reads attribute values from a custom field JSON object as invariant strings.</summary>
    /// <param name="json">The JSON object.</param>
    /// <returns>The values keyed by attribute name.</returns>
    public static Dictionary<string, string?> ParseValues(string? json)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        try
        {
            if (JsonNode.Parse(json) is JsonObject obj)
            {
                foreach (var (key, node) in obj)
                {
                    values[key] = node switch
                    {
                        null => null,
                        JsonValue v when v.TryGetValue<string>(out var s) => s,
                        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
                        _ => node.ToJsonString(),
                    };
                }
            }
        }
        catch (JsonException)
        {
            // Corrupt custom field JSON is treated as empty.
        }

        return values;
    }

    /// <summary>
    /// Validates attribute values against definitions and merges them into an existing custom field JSON object. Values of
    /// attributes that are not defined for the class are preserved untouched (they may belong to platform custom fields).
    /// </summary>
    /// <param name="definitions">The effective attribute definitions of the class.</param>
    /// <param name="values">The submitted values keyed by attribute name.</param>
    /// <param name="existingJson">The current custom field JSON.</param>
    /// <param name="json">The merged JSON.</param>
    /// <returns>An error message, or <see langword="null"/> when valid.</returns>
    public static string? ApplyValues(IReadOnlyList<CiAttributeDefinition> definitions, IReadOnlyDictionary<string, string?> values, string? existingJson, out string? json)
    {
        json = existingJson;
        JsonObject obj;
        try
        {
            obj = (string.IsNullOrWhiteSpace(existingJson) ? null : JsonNode.Parse(existingJson) as JsonObject) ?? new JsonObject();
        }
        catch (JsonException)
        {
            obj = new JsonObject();
        }

        foreach (var definition in definitions)
        {
            var raw = values.GetValueOrDefault(definition.Name)?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                if (definition.Required)
                {
                    return $"{definition.Label} is required.";
                }

                obj.Remove(definition.Name);
                continue;
            }

            if (raw.Length > MaxValueLength)
            {
                return $"{definition.Label} must not exceed {MaxValueLength} characters.";
            }

            switch (definition.Type)
            {
                case CiAttributeType.Number:
                    if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    {
                        return $"{definition.Label} must be a number.";
                    }

                    obj[definition.Name] = number.ToString(CultureInfo.InvariantCulture);
                    break;
                case CiAttributeType.Boolean:
                    if (!bool.TryParse(raw, out var flag))
                    {
                        return $"{definition.Label} must be true or false.";
                    }

                    obj[definition.Name] = flag;
                    break;
                case CiAttributeType.Date:
                    if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        return $"{definition.Label} must be a date (yyyy-MM-dd).";
                    }

                    obj[definition.Name] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    break;
                case CiAttributeType.Choice:
                    var choice = definition.Choices.FirstOrDefault(c => string.Equals(c, raw, StringComparison.OrdinalIgnoreCase));
                    if (choice is null)
                    {
                        return $"{definition.Label} must be one of: {string.Join(", ", definition.Choices)}.";
                    }

                    obj[definition.Name] = choice;
                    break;
                default:
                    obj[definition.Name] = raw;
                    break;
            }
        }

        json = obj.Count == 0 ? null : obj.ToJsonString();
        return json?.Length > 32_000 ? "The attribute values are too large." : null;
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();
}
