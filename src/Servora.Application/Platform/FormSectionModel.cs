namespace Servora.Application.Platform;

/// <summary>A section of a generic form with its ordered fields.</summary>
public sealed class FormSectionModel
{
    /// <summary>Gets or sets the section title.</summary>
    public string Title { get; set; } = "General";

    /// <summary>Gets or sets the ordered field names.</summary>
    public List<string> Fields { get; set; } = [];
}
