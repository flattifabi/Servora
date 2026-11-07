using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Servora.Application.Automation.Model;

/// <summary>Serialization helpers for flow definitions and their structured step settings.</summary>
public static class FlowJson
{
    /// <summary>The maximum number of steps in one flow (all branches included).</summary>
    public const int MaxSteps = 200;

    /// <summary>The maximum branch nesting depth.</summary>
    public const int MaxNesting = 8;

    /// <summary>Gets the shared serializer options (web defaults, enums as strings).</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 64,
    };

    /// <summary>Serializes a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Deserializes a value; invalid or empty JSON yields <see langword="null"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="json">The JSON text.</param>
    /// <returns>The value or <see langword="null"/>.</returns>
    public static T? TryDeserialize<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Deserializes the step tree of a flow (never null).</summary>
    /// <param name="json">The steps JSON.</param>
    /// <returns>The steps.</returns>
    public static List<FlowStepDefinition> DeserializeSteps(string? json) => TryDeserialize<List<FlowStepDefinition>>(json) ?? [];

    /// <summary>Deserializes the input definitions of a flow (never null).</summary>
    /// <param name="json">The inputs JSON.</param>
    /// <returns>The inputs.</returns>
    public static List<FlowInputDefinition> DeserializeInputs(string? json) => TryDeserialize<List<FlowInputDefinition>>(json) ?? [];

    /// <summary>Converts a CLR value to a JSON node (null-safe).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The node.</returns>
    public static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node.DeepClone(),
        JsonElement element => JsonNode.Parse(element.GetRawText()),
        _ => JsonSerializer.SerializeToNode(value, Options),
    };

    /// <summary>Enumerates all steps of a tree depth-first in execution order.</summary>
    /// <param name="steps">The root steps.</param>
    /// <returns>All steps.</returns>
    public static IEnumerable<FlowStepDefinition> Flatten(IEnumerable<FlowStepDefinition> steps)
    {
        foreach (var step in steps)
        {
            yield return step;
            foreach (var branch in step.Branches)
            {
                foreach (var child in Flatten(branch.Steps))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>Gets the maximum nesting depth of a step tree (1 = no branches with steps).</summary>
    /// <param name="steps">The steps.</param>
    /// <returns>The depth.</returns>
    public static int GetDepth(IEnumerable<FlowStepDefinition> steps)
    {
        var max = 0;
        foreach (var step in steps)
        {
            var depth = 1 + step.Branches.Select(b => GetDepth(b.Steps)).DefaultIfEmpty(0).Max();
            max = Math.Max(max, depth);
        }

        return max;
    }
}
