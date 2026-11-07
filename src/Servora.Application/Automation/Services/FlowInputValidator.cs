using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Automation.Model;
using Servora.Application.Common;

namespace Servora.Application.Automation.Services;

/// <summary>Validates and converts input values of manual starts and test runs against the flow's input definitions.</summary>
public static class FlowInputValidator
{
    /// <summary>The maximum length of a single input value.</summary>
    public const int MaxValueLength = 10_000;

    /// <summary>Validates input values.</summary>
    /// <param name="definitions">The input definitions.</param>
    /// <param name="values">The supplied values.</param>
    /// <returns>The typed inputs as JSON object, or a validation failure.</returns>
    public static Result<JsonObject> Validate(IReadOnlyList<FlowInputDefinition> definitions, IReadOnlyDictionary<string, object?>? values)
    {
        values ??= new Dictionary<string, object?>();
        if (values.Count > FlowDefinitionValidator.MaxInputs)
        {
            return Result<JsonObject>.Invalid("Too many input values.");
        }

        foreach (var key in values.Keys)
        {
            if (!definitions.Any(d => string.Equals(d.Name, key, StringComparison.OrdinalIgnoreCase)))
            {
                return Result<JsonObject>.Invalid($"The input '{key}' is not defined by this flow.");
            }
        }

        var result = new JsonObject();
        foreach (var definition in definitions)
        {
            var supplied = values.FirstOrDefault(v => string.Equals(v.Key, definition.Name, StringComparison.OrdinalIgnoreCase)).Value;
            var text = supplied switch
            {
                null => null,
                string s => s,
                JsonElement element => element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => supplied.ToString(),
            };
            if (string.IsNullOrEmpty(text))
            {
                text = definition.DefaultValue;
            }

            if (string.IsNullOrEmpty(text))
            {
                if (definition.Required)
                {
                    return Result<JsonObject>.Invalid($"The input '{definition.Label}' is required.");
                }

                continue;
            }

            if (text.Length > MaxValueLength)
            {
                return Result<JsonObject>.Invalid($"The input '{definition.Label}' is too long.");
            }

            switch (definition.Type.ToLowerInvariant())
            {
                case "number":
                    if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    {
                        return Result<JsonObject>.Invalid($"The input '{definition.Label}' must be a number.");
                    }

                    result[definition.Name] = number;
                    break;
                case "boolean":
                    if (!bool.TryParse(text, out var flag))
                    {
                        return Result<JsonObject>.Invalid($"The input '{definition.Label}' must be true or false.");
                    }

                    result[definition.Name] = flag;
                    break;
                case "record":
                    if (!Guid.TryParse(text, out var id))
                    {
                        return Result<JsonObject>.Invalid($"The input '{definition.Label}' must be a record id.");
                    }

                    result[definition.Name] = id.ToString();
                    break;
                default:
                    result[definition.Name] = text;
                    break;
            }
        }

        return Result<JsonObject>.Success(result);
    }
}
