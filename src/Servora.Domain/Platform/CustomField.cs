using Servora.Domain.Common;

namespace Servora.Domain.Platform;

/// <summary>
/// An administrator-defined field, either on a typed table (values stored in <see cref="IHasCustomFields.CustomFieldsJson"/>)
/// or a column of a <see cref="CustomTable"/> (values stored in <see cref="CustomRecord.DataJson"/>).
/// </summary>
public class CustomField : AuditableEntity
{
    /// <summary>Gets or sets the technical name of the table the field belongs to.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the technical field name. Always starts with <c>u_</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the data type.</summary>
    public CustomFieldType Type { get; set; } = CustomFieldType.String;

    /// <summary>Gets or sets the choices of a <see cref="CustomFieldType.Choice"/> field as JSON array of <c>{ "value", "label" }</c>.</summary>
    public string? ChoicesJson { get; set; }

    /// <summary>Gets or sets the referenced table of a <see cref="CustomFieldType.Reference"/> field.</summary>
    public string? ReferenceTable { get; set; }

    /// <summary>Gets or sets a value indicating whether a value is mandatory.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Gets or sets the default value as invariant string.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Gets or sets the help text shown below the input.</summary>
    public string? HelpText { get; set; }

    /// <summary>Gets or sets the maximum length of text values.</summary>
    public int? MaxLength { get; set; }

    /// <summary>Gets or sets the sort order within the table's field list.</summary>
    public int Order { get; set; }
}
