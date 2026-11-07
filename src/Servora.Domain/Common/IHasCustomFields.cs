namespace Servora.Domain.Common;

/// <summary>
/// Marks an entity that can carry administrator-defined custom fields. Custom field values are stored as a
/// JSON object so that adding fields through the UI never requires a schema migration.
/// </summary>
public interface IHasCustomFields
{
    /// <summary>Gets or sets the JSON object that holds custom field values keyed by field name.</summary>
    string? CustomFieldsJson { get; set; }
}
