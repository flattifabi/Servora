namespace Servora.Application.Tables;

/// <summary>Metadata of a platform table (typed entity or custom table).</summary>
public sealed record TableDefinition
{
    /// <summary>Gets the technical table name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the singular label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the plural label.</summary>
    public required string PluralLabel { get; init; }

    /// <summary>Gets the field used as display value.</summary>
    public required string DisplayField { get; init; }

    /// <summary>Gets the icon name.</summary>
    public string Icon { get; init; } = "table";

    /// <summary>Gets the UI route of the list view.</summary>
    public string? Route { get; init; }

    /// <summary>Gets a value indicating whether the table is an administrator-defined custom table.</summary>
    public bool IsCustom { get; init; }

    /// <summary>Gets the CLR entity type for typed tables (null for custom tables).</summary>
    public Type? EntityType { get; init; }

    /// <summary>Gets the fields.</summary>
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = [];

    /// <summary>Finds a field by name (case-insensitive).</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The field or <see langword="null"/>.</returns>
    public FieldDefinition? FindField(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
}
