namespace Servora.Application.Knowledge;

/// <summary>Editable fields of a knowledge category.</summary>
public sealed class KnowledgeCategoryInput
{
    /// <summary>Gets or sets the owning knowledge base.</summary>
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name.</summary>
    public string Icon { get; set; } = "folder";

    /// <summary>Gets or sets the display order.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether the category is active.</summary>
    public bool IsActive { get; set; } = true;
}
