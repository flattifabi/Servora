using System.Text.Json;
using Servora.Domain.Common;

namespace Servora.Domain.Platform;

/// <summary>
/// A record of an administrator-defined <see cref="CustomTable"/>. Field values are stored as a JSON object in
/// <see cref="DataJson"/>; a searchable copy is kept in <see cref="RecordFieldValue"/> rows so conditions, search and
/// sorting run in the database on every provider. Write custom records only through the generic record service.
/// </summary>
public class CustomRecord : AuditableEntity, IDynamicTableRecord
{
    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the record number (only for tables with a number prefix).</summary>
    public string? Number { get; set; }

    /// <summary>Gets or sets the denormalized display value (value of the table's display field).</summary>
    public string? DisplayValue { get; set; }

    /// <summary>Gets or sets the field values as JSON object keyed by field name.</summary>
    public string DataJson { get; set; } = "{}";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> GetFieldValues()
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(DataJson) ? "{}" : DataJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    values[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.TryGetInt64(out var l) ? l : property.Value.GetDecimal(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null or JsonValueKind.Undefined => null,
                        _ => property.Value.GetRawText(),
                    };
                }
            }
        }
        catch (JsonException)
        {
            // Invalid data is treated as empty; the record service only ever writes valid JSON.
        }

        values[nameof(Id)] = Id;
        values[nameof(Number)] = Number;
        values[nameof(CreatedAt)] = CreatedAt;
        values[nameof(CreatedBy)] = CreatedBy;
        values[nameof(UpdatedAt)] = UpdatedAt;
        values[nameof(UpdatedBy)] = UpdatedBy;
        return values;
    }
}
