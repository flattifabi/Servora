using System.Text.Json;
using Servora.Domain.Common;
using Servora.Domain.Platform;

namespace Servora.Application.Tables;

/// <summary>Builds <see cref="TableDefinition"/>s and <see cref="FieldDefinition"/>s from custom table metadata.</summary>
public static class CustomTableDefinitionBuilder
{
    /// <summary>The field name of the record number of custom tables with a number prefix.</summary>
    public const string NumberField = "Number";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the default maximum length of a text field type.</summary>
    /// <param name="type">The field type.</param>
    /// <returns>The maximum length or <see langword="null"/> for non-text types.</returns>
    public static int? DefaultMaxLength(FieldType type) => type switch
    {
        FieldType.String => 255,
        FieldType.Text => 4000,
        FieldType.Email => 254,
        FieldType.Url => 2000,
        FieldType.Json => 16_000,
        FieldType.Choice => 100,
        _ => null,
    };

    /// <summary>Converts a custom field into a field definition.</summary>
    /// <param name="field">The custom field.</param>
    /// <returns>The definition.</returns>
    public static FieldDefinition ToField(CustomField field)
    {
        var type = (FieldType)(int)field.Type;
        return new FieldDefinition
        {
            Name = field.Name,
            Label = field.Label,
            Type = type,
            ReferenceTable = type == FieldType.Reference ? field.ReferenceTable : null,
            Choices = type == FieldType.Choice ? ParseChoices(field.ChoicesJson) : [],
            IsRequired = field.IsRequired,
            IsCustom = true,
            MaxLength = field.MaxLength ?? DefaultMaxLength(type),
            DefaultValue = field.DefaultValue,
            HelpText = field.HelpText,
        };
    }

    /// <summary>Builds the definition of a custom table.</summary>
    /// <param name="table">The table metadata.</param>
    /// <param name="fields">The custom fields of the table.</param>
    /// <returns>The definition.</returns>
    public static TableDefinition BuildCustomTable(CustomTable table, IEnumerable<CustomField> fields)
    {
        var definitions = new List<FieldDefinition>
        {
            System(nameof(Entity.Id), "Sys ID", FieldType.Guid),
        };
        if (!string.IsNullOrWhiteSpace(table.NumberPrefix))
        {
            definitions.Add(System(NumberField, "Number", FieldType.String) with { MaxLength = 40 });
        }

        definitions.AddRange(fields.OrderBy(f => f.Order).ThenBy(f => f.Label).Select(ToField));
        definitions.Add(System(nameof(AuditableEntity.CreatedAt), "Created", FieldType.DateTime));
        definitions.Add(System(nameof(AuditableEntity.CreatedBy), "Created by", FieldType.String));
        definitions.Add(System(nameof(AuditableEntity.UpdatedAt), "Updated", FieldType.DateTime));
        definitions.Add(System(nameof(AuditableEntity.UpdatedBy), "Updated by", FieldType.String));

        var displayField = !string.IsNullOrWhiteSpace(table.DisplayField) && definitions.Any(d => string.Equals(d.Name, table.DisplayField, StringComparison.OrdinalIgnoreCase))
            ? definitions.First(d => string.Equals(d.Name, table.DisplayField, StringComparison.OrdinalIgnoreCase)).Name
            : !string.IsNullOrWhiteSpace(table.NumberPrefix)
                ? NumberField
                : definitions.FirstOrDefault(d => d.IsCustom && d.Type == FieldType.String)?.Name ?? nameof(Entity.Id);

        return new TableDefinition
        {
            Name = table.Name,
            Label = table.Label,
            PluralLabel = table.PluralLabel,
            DisplayField = displayField,
            Icon = string.IsNullOrWhiteSpace(table.Icon) ? "table" : table.Icon,
            Route = "/table/" + table.Name,
            IsCustom = true,
            Fields = definitions,
        };
    }

    /// <summary>Adds custom fields to a typed table definition.</summary>
    /// <param name="typed">The typed table.</param>
    /// <param name="fields">The custom fields.</param>
    /// <returns>The merged definition.</returns>
    public static TableDefinition WithCustomFields(TableDefinition typed, IEnumerable<CustomField> fields)
    {
        var custom = fields.OrderBy(f => f.Order).ThenBy(f => f.Label)
            .Where(f => typed.FindField(f.Name) is null)
            .Select(ToField)
            .ToList();
        return custom.Count == 0 ? typed : typed with { Fields = [.. typed.Fields, .. custom] };
    }

    /// <summary>Parses the choices JSON of a custom field.</summary>
    /// <param name="json">The JSON array.</param>
    /// <returns>The choices.</returns>
    public static IReadOnlyList<FieldChoice> ParseChoices(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<FieldChoice>>(json, s_json) ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => new FieldChoice(c.Value, string.IsNullOrWhiteSpace(c.Label) ? c.Value : c.Label))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Serializes choices for storage.</summary>
    /// <param name="choices">The choices.</param>
    /// <returns>The JSON array.</returns>
    public static string SerializeChoices(IEnumerable<FieldChoice> choices) => JsonSerializer.Serialize(choices, s_json);

    private static FieldDefinition System(string name, string label, FieldType type) => new()
    {
        Name = name,
        Label = label,
        Type = type,
        IsReadOnly = true,
    };
}
