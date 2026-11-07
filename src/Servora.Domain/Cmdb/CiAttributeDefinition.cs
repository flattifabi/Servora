namespace Servora.Domain.Cmdb;

/// <summary>Definition of one class-specific attribute of a <see cref="CiClass"/> (element of <see cref="CiClass.AttributesJson"/>).</summary>
public sealed class CiAttributeDefinition
{
    /// <summary>Gets or sets the technical name (letters, digits and underscores).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the data type.</summary>
    public CiAttributeType Type { get; set; } = CiAttributeType.Text;

    /// <summary>Gets or sets a value indicating whether a value is mandatory.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets the allowed values for <see cref="CiAttributeType.Choice"/> attributes.</summary>
    public List<string> Choices { get; set; } = [];

    /// <summary>Gets or sets an optional help text.</summary>
    public string? Help { get; set; }

    /// <summary>Gets or sets an optional unit shown after numbers (for example <c>GB</c>).</summary>
    public string? Unit { get; set; }
}
