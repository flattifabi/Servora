namespace Servora.Application.Platform;

/// <summary>Form layout and default list columns of a table.</summary>
public sealed class TableLayoutModel
{
    /// <summary>Gets or sets the form sections.</summary>
    public List<FormSectionModel> Sections { get; set; } = [];

    /// <summary>Gets or sets the default list columns.</summary>
    public List<string> ListColumns { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the layout was customized (false = generated default).</summary>
    public bool IsCustomized { get; set; }
}
