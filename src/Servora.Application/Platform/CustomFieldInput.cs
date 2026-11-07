using Servora.Application.Tables;

namespace Servora.Application.Platform;

/// <summary>Editable properties of a custom field.</summary>
public sealed class CustomFieldInput
{
    /// <summary>Gets or sets the technical name (<c>u_</c> prefix; only used on creation).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the data type (cannot be changed after creation).</summary>
    public FieldType Type { get; set; } = FieldType.String;

    /// <summary>Gets or sets the choices of choice fields.</summary>
    public List<FieldChoice> Choices { get; set; } = [];

    /// <summary>Gets or sets the referenced table of reference fields.</summary>
    public string? ReferenceTable { get; set; }

    /// <summary>Gets or sets a value indicating whether the field is mandatory.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Gets or sets the default value.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Gets or sets the help text.</summary>
    public string? HelpText { get; set; }

    /// <summary>Gets or sets the maximum length of text fields.</summary>
    public int? MaxLength { get; set; }

    /// <summary>Gets or sets the sort order.</summary>
    public int Order { get; set; }
}
