namespace Servora.Application.Platform;

/// <summary>Editable properties of a custom table.</summary>
public sealed class CustomTableInput
{
    /// <summary>Gets or sets the technical name (<c>u_</c> prefix; only used on creation).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the singular label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the plural label (defaults to the label with an "s").</summary>
    public string? PluralLabel { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = "table";

    /// <summary>Gets or sets the display field.</summary>
    public string? DisplayField { get; set; }

    /// <summary>Gets or sets the optional record number prefix (2-8 upper-case letters).</summary>
    public string? NumberPrefix { get; set; }

    /// <summary>Gets or sets a value indicating whether the table accepts new records.</summary>
    public bool IsActive { get; set; } = true;
}
