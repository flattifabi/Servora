using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Conditions;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>
/// Reads and writes field values of tracked entities generically: typed properties, custom fields of typed tables
/// (<see cref="IHasCustomFields.CustomFieldsJson"/>) and columns of custom tables (<see cref="CustomRecord.DataJson"/>).
/// Only fields of the <see cref="TableDefinition"/> are touched, so <see cref="NotExposedAttribute"/> properties are never read or written.
/// </summary>
public static class RecordEntityAccessor
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> s_properties = new();

    /// <summary>Reads the values of all fields of a table from an entity.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="entity">The entity (typed entity or <see cref="CustomRecord"/>).</param>
    /// <returns>The values keyed by field name (case-insensitive).</returns>
    public static Dictionary<string, object?> ReadValues(TableDefinition table, object entity)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (entity is CustomRecord record)
        {
            var data = record.GetFieldValues();
            foreach (var field in table.Fields)
            {
                var raw = data.GetValueOrDefault(field.Name);
                values[field.Name] = field.IsCustom ? ToCanonical(field, raw) : raw;
            }

            return values;
        }

        Dictionary<string, object?>? custom = null;
        foreach (var field in table.Fields)
        {
            if (field.IsCustom)
            {
                custom ??= ParseObject((entity as IHasCustomFields)?.CustomFieldsJson);
                values[field.Name] = ToCanonical(field, custom.GetValueOrDefault(field.Name));
                continue;
            }

            if (FindProperty(entity.GetType(), field.Name) is { } property)
            {
                values[field.Name] = property.GetValue(entity);
            }
        }

        return values;
    }

    /// <summary>
    /// Restricts a record event snapshot to the fields of the table definition (never <see cref="NotExposedAttribute"/>
    /// properties) and converts custom field values to their canonical types.
    /// </summary>
    /// <param name="table">The table definition.</param>
    /// <param name="snapshot">The snapshot of a record event.</param>
    /// <returns>The exposed values keyed by field name (case-insensitive).</returns>
    public static Dictionary<string, object?> FilterExposed(TableDefinition table, IReadOnlyDictionary<string, object?> snapshot)
    {
        var lookup = snapshot is Dictionary<string, object?> d && ReferenceEquals(d.Comparer, StringComparer.OrdinalIgnoreCase)
            ? snapshot
            : snapshot.GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in table.Fields)
        {
            var raw = lookup.GetValueOrDefault(field.Name);
            values[field.Name] = field.IsCustom ? ToCanonical(field, raw) : ConditionValueConverter.Normalize(raw);
        }

        return values;
    }

    /// <summary>Writes an already converted value (see <see cref="RecordValueConverter"/>) into an entity.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="field">The field.</param>
    /// <param name="value">The typed value.</param>
    /// <returns><see langword="true"/> when the value was written.</returns>
    public static bool WriteValue(object entity, FieldDefinition field, object? value)
    {
        if (entity is CustomRecord record)
        {
            if (!field.IsCustom)
            {
                return false;
            }

            record.DataJson = SetJsonValue(record.DataJson, field.Name, value);
            return true;
        }

        if (field.IsCustom)
        {
            if (entity is not IHasCustomFields hasCustomFields)
            {
                return false;
            }

            hasCustomFields.CustomFieldsJson = SetJsonValue(hasCustomFields.CustomFieldsJson, field.Name, value);
            return true;
        }

        if (FindProperty(entity.GetType(), field.Name) is not { SetMethod.IsPublic: true } property)
        {
            return false;
        }

        property.SetValue(entity, value);
        return true;
    }

    /// <summary>Gets the CLR type of a typed field, or <see langword="null"/> for custom fields.</summary>
    /// <param name="table">The table.</param>
    /// <param name="field">The field.</param>
    /// <returns>The property type or <see langword="null"/>.</returns>
    public static Type? GetClrType(TableDefinition table, FieldDefinition field) =>
        field.IsCustom || table.EntityType is null ? null : FindProperty(table.EntityType, field.Name)?.PropertyType;

    /// <summary>Converts a JSON-normalized custom field value into its canonical CLR type.</summary>
    /// <param name="field">The field.</param>
    /// <param name="raw">The raw value.</param>
    /// <returns>The canonical value (or the raw value when it cannot be converted).</returns>
    public static object? ToCanonical(FieldDefinition field, object? raw)
    {
        raw = ConditionValueConverter.Normalize(raw);
        if (raw is null)
        {
            return null;
        }

        var text = ConditionValueConverter.ToText(raw);
        switch (field.Type)
        {
            case FieldType.Integer:
                return raw is long ? raw : long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : raw;
            case FieldType.Decimal:
                return decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var d) ? d : raw;
            case FieldType.Boolean:
                return raw is bool ? raw : ConditionValueConverter.TryParseBoolean(text, out var b) ? b : raw;
            case FieldType.DateTime:
                return ConditionValueConverter.TryParseDate(text, out var dto, out _) ? dto : raw;
            case FieldType.Date:
                return ConditionValueConverter.TryParseDate(text, out var date, out _) ? DateOnly.FromDateTime(date.UtcDateTime) : raw;
            case FieldType.Reference or FieldType.Guid:
                return Guid.TryParse(text, out var g) ? g : raw;
            default:
                return text;
        }
    }

    /// <summary>Parses a JSON object into JSON-normalized values.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The values (empty for null or invalid JSON).</returns>
    public static Dictionary<string, object?> ParseObject(string? json)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    values[property.Name] = ConditionValueConverter.Normalize(property.Value.Clone());
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON is treated as empty.
        }

        return values;
    }

    /// <summary>Sets (or removes, for null) a value in a JSON object.</summary>
    /// <param name="json">The JSON object text.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The canonical value.</param>
    /// <returns>The new JSON text.</returns>
    public static string SetJsonValue(string? json, string name, object? value)
    {
        JsonObject root;
        try
        {
            root = (string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject) ?? [];
        }
        catch (JsonException)
        {
            root = [];
        }

        var existing = root.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            root.Remove(existing);
        }

        var node = ToJsonNode(value);
        if (node is not null)
        {
            root[name] = node;
        }

        return root.ToJsonString();
    }

    private static JsonNode? ToJsonNode(object? value) => ConditionValueConverter.Normalize(value) switch
    {
        null => null,
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        long l => JsonValue.Create(l),
        int i => JsonValue.Create(i),
        decimal m => JsonValue.Create(m),
        double dbl => JsonValue.Create(dbl),
        Guid g => JsonValue.Create(g.ToString("D")),
        DateTimeOffset dto => JsonValue.Create(dto.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        DateTime dt => JsonValue.Create(dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        DateOnly d => JsonValue.Create(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        var other => JsonValue.Create(ConditionValueConverter.ToText(other)),
    };

    private static PropertyInfo? FindProperty(Type type, string name) =>
        s_properties.GetOrAdd((type, name), key =>
        {
            var property = key.Type.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return property is null || property.GetCustomAttribute<NotExposedAttribute>() is not null || property.GetIndexParameters().Length > 0 ? null : property;
        });
}
