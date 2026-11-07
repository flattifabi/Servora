using Servora.Domain.Common;

namespace Servora.Domain.Knowledge;

/// <summary>A category inside a knowledge base used to browse articles.</summary>
[RecordTable(KnowledgeTables.Category, "Knowledge Category", PluralLabel = "Knowledge Categories", DisplayField = nameof(Name), Icon = "folder")]
public class KnowledgeCategory : AuditableEntity
{
    /// <summary>Gets or sets the owning knowledge base.</summary>
    [Reference(KnowledgeTables.KnowledgeBase)]
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>Gets or sets the category name.</summary>
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
