namespace Servora.Application.Tables;

/// <summary>Metadata of a table field (typed property, custom field on a typed table, or custom table column).</summary>
public sealed record FieldDefinition
{
    /// <summary>Gets the field name (property name for typed tables).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the display label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the data type.</summary>
    public required FieldType Type { get; init; }

    /// <summary>Gets the referenced table for <see cref="FieldType.Reference"/> fields.</summary>
    public string? ReferenceTable { get; init; }

    /// <summary>Gets the choices for <see cref="FieldType.Choice"/> fields.</summary>
    public IReadOnlyList<FieldChoice> Choices { get; init; } = [];

    /// <summary>Gets a value indicating whether the field is mandatory.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Gets a value indicating whether the field is read only (system maintained).</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Gets a value indicating whether the field is an administrator-defined custom field.</summary>
    public bool IsCustom { get; init; }

    /// <summary>Gets the maximum length for text fields.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Gets the default value as invariant string.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets the help text shown in forms.</summary>
    public string? HelpText { get; init; }
}
