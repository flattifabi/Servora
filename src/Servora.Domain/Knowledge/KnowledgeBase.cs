using Servora.Domain.Common;

namespace Servora.Domain.Knowledge;

/// <summary>A knowledge base: a top-level collection of articles, for example "IT Knowledge" or "HR Knowledge".</summary>
[RecordTable(KnowledgeTables.KnowledgeBase, "Knowledge Base", PluralLabel = "Knowledge Bases", DisplayField = nameof(Name), Icon = "book-open", Route = "/knowledge")]
public class KnowledgeBase : AuditableEntity
{
    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description shown on the knowledge home page.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the icon name of the design system icon set.</summary>
    public string Icon { get; set; } = "book-open";

    /// <summary>Gets or sets the group that owns (curates) the knowledge base.</summary>
    [Reference(TableNames.Group)]
    public Guid? OwnerGroupId { get; set; }

    /// <summary>Gets or sets a value indicating whether the knowledge base is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the display order.</summary>
    public int SortOrder { get; set; }
}
