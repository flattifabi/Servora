using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Servora.Domain.Common;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Builds the field value snapshot of a tracked record for record events and condition evaluation. Custom field values
/// (stored in <see cref="IHasCustomFields.CustomFieldsJson"/>) are flattened into the snapshot by field name.
/// </summary>
internal static class RecordSnapshotBuilder
{
    /// <summary>Builds the snapshot of an entry.</summary>
    /// <param name="entry">The change tracker entry.</param>
    /// <returns>The values keyed by field name (case-insensitive).</returns>
    public static IReadOnlyDictionary<string, object?> Build(EntityEntry entry)
    {
        if (entry.Entity is IDynamicTableRecord dynamic)
        {
            return new Dictionary<string, object?>(dynamic.GetFieldValues(), StringComparer.OrdinalIgnoreCase);
        }

        var snapshot = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in entry.Properties)
        {
            snapshot[property.Metadata.Name] = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
        }

        if (entry.Entity is IHasCustomFields { CustomFieldsJson: { Length: > 0 } json })
        {
            FlattenCustomFields(json, snapshot);
        }

        return snapshot;
    }

    /// <summary>Converts a JSON element into a primitive CLR value.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The value.</returns>
    public static object? NormalizeJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.GetRawText(),
    };

    private static void FlattenCustomFields(string json, Dictionary<string, object?> snapshot)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                snapshot.TryAdd(property.Name, NormalizeJson(property.Value));
            }
        }
        catch (JsonException)
        {
            // Invalid custom field JSON is ignored for snapshots; validation happens when values are written.
        }
    }
}
