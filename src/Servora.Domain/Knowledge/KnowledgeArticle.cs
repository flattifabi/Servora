using Servora.Domain.Common;

namespace Servora.Domain.Knowledge;

/// <summary>
/// A knowledge article (<c>kb_knowledge</c>). The body is Markdown and is always rendered through the safe Markdown renderer.
/// The workflow is Draft → Review → Published → Retired (see <see cref="KnowledgeWorkflow"/>).
/// </summary>
[RecordTable(TableNames.KnowledgeArticle, "Knowledge Article", PluralLabel = "Knowledge Articles", DisplayField = nameof(Number), Icon = "book-open", Route = "/knowledge")]
public class KnowledgeArticle : AuditableEntity, IHasCustomFields
{
    /// <summary>Gets or sets the human readable number, for example <c>KB0001001</c>.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the short summary shown in search results.</summary>
    public string? Summary { get; set; }

    /// <summary>Gets or sets the Markdown body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the knowledge base.</summary>
    [Reference(KnowledgeTables.KnowledgeBase)]
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>Gets or sets the category.</summary>
    [Reference(KnowledgeTables.Category)]
    public Guid? CategoryId { get; set; }

    /// <summary>Gets or sets the workflow state.</summary>
    public KnowledgeArticleState State { get; set; } = KnowledgeArticleState.Draft;

    /// <summary>Gets or sets the author.</summary>
    [Reference(TableNames.User)]
    public Guid? AuthorId { get; set; }

    /// <summary>Gets or sets the display name of the author (kept for display when the user is removed).</summary>
    public string? AuthorName { get; set; }

    /// <summary>Gets or sets the reviewer that published or rejected the article last.</summary>
    [Reference(TableNames.User)]
    public Guid? ReviewedById { get; set; }

    /// <summary>Gets or sets the reviewer comment of the last review decision.</summary>
    public string? ReviewComment { get; set; }

    /// <summary>Gets or sets the UTC time the article was (last) published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Gets or sets the UTC time the article was retired.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    /// <summary>Gets or sets the date after which the article is no longer shown to readers.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>Gets or sets the content version number (incremented with every saved content change).</summary>
    public int ArticleVersion { get; set; } = 1;

    /// <summary>Gets or sets the number of reader views.</summary>
    public int ViewCount { get; set; }

    /// <summary>Gets or sets the number of "helpful" votes.</summary>
    public int HelpfulCount { get; set; }

    /// <summary>Gets or sets the number of "not helpful" votes.</summary>
    public int NotHelpfulCount { get; set; }

    /// <summary>Gets or sets comma separated keywords used to boost search ranking.</summary>
    public string? Keywords { get; set; }

    /// <inheritdoc />
    public string? CustomFieldsJson { get; set; }
}
