namespace Servora.Application.Knowledge;

/// <summary>Editable fields of a knowledge base.</summary>
public sealed class KnowledgeBaseInput
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = "book-open";

    /// <summary>Gets or sets a value indicating whether the knowledge base is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the display order.</summary>
    public int SortOrder { get; set; }
}
