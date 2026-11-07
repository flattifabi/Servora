using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servora.Application.Conditions;

/// <summary>Serializes <see cref="ConditionGroup"/>s to and from their stored JSON representation.</summary>
public static class ConditionSerializer
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes a condition.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The JSON string, or <see langword="null"/> for null / empty conditions.</returns>
    public static string? Serialize(ConditionGroup? condition) =>
        condition is null || condition.IsEmpty ? null : JsonSerializer.Serialize(condition, s_options);

    /// <summary>Deserializes a condition. Invalid JSON yields an empty condition.</summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The condition (never null).</returns>
    public static ConditionGroup Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ConditionGroup();
        }

        try
        {
            return JsonSerializer.Deserialize<ConditionGroup>(json, s_options) ?? new ConditionGroup();
        }
        catch (JsonException)
        {
            return new ConditionGroup();
        }
    }
}
