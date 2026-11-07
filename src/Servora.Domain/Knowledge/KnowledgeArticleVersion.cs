using Servora.Domain.Common;

namespace Servora.Domain.Knowledge;

/// <summary>Immutable snapshot of an article's content, written whenever the content changes.</summary>
public class KnowledgeArticleVersion : Entity
{
    /// <summary>Gets or sets the article.</summary>
    [Reference(TableNames.KnowledgeArticle)]
    public Guid ArticleId { get; set; }

    /// <summary>Gets or sets the content version number.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Gets or sets the title at that version.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the summary at that version.</summary>
    public string? Summary { get; set; }

    /// <summary>Gets or sets the Markdown body at that version.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the state of the article when the snapshot was taken.</summary>
    public KnowledgeArticleState State { get; set; }

    /// <summary>Gets or sets an optional change note.</summary>
    public string? ChangeNote { get; set; }

    /// <summary>Gets or sets the display name of the editor.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets the UTC time of the snapshot.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
