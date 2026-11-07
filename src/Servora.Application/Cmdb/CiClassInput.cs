using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>Editable fields of a CI class.</summary>
public sealed class CiClassInput
{
    /// <summary>Gets or sets the technical key (only used when creating; keys are immutable).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = "box";

    /// <summary>Gets or sets the accent color (<c>#rrggbb</c>).</summary>
    public string Color { get; set; } = "#7c6cff";

    /// <summary>Gets or sets the parent class key.</summary>
    public string? ParentKey { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the display order.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the attribute definitions of this class.</summary>
    public List<CiAttributeDefinition> Attributes { get; set; } = [];
}
