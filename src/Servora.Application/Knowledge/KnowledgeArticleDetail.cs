using Servora.Application.Directory;
using Servora.Application.Knowledge.Markdown;
using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>Full article for the reader and editor, including rendered HTML and the caller's capabilities.</summary>
public sealed class KnowledgeArticleDetail
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the number.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the summary.</summary>
    public string? Summary { get; set; }

    /// <summary>Gets or sets the Markdown body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the sanitized HTML of the body.</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>Gets or sets the headings (table of contents).</summary>
    public IReadOnlyList<MarkdownHeading> Headings { get; set; } = [];

    /// <summary>Gets or sets the knowledge base.</summary>
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>Gets or sets the knowledge base name.</summary>
    public string KnowledgeBaseName { get; set; } = string.Empty;

    /// <summary>Gets or sets the category.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Gets or sets the category name.</summary>
    public string? CategoryName { get; set; }

    /// <summary>Gets or sets the state.</summary>
    public KnowledgeArticleState State { get; set; }

    /// <summary>Gets or sets the author name.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Gets or sets the reviewer comment.</summary>
    public string? ReviewComment { get; set; }

    /// <summary>Gets or sets the publication time.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Gets or sets the retirement time.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    /// <summary>Gets or sets the validity end date.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>Gets or sets the keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Gets or sets the content version.</summary>
    public int ArticleVersion { get; set; }

    /// <summary>Gets or sets the view count.</summary>
    public int ViewCount { get; set; }

    /// <summary>Gets or sets the helpful vote count.</summary>
    public int HelpfulCount { get; set; }

    /// <summary>Gets or sets the not helpful vote count.</summary>
    public int NotHelpfulCount { get; set; }

    /// <summary>Gets or sets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the last editor.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Gets or sets the concurrency version.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the related configuration items.</summary>
    public IReadOnlyList<LookupItem> RelatedCis { get; set; } = [];

    /// <summary>Gets or sets the current user's vote (<see langword="null"/> when not voted).</summary>
    public bool? MyVote { get; set; }

    /// <summary>Gets or sets a value indicating whether the caller may edit the content.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Gets or sets a value indicating whether the caller may delete the article.</summary>
    public bool CanDelete { get; set; }

    /// <summary>Gets or sets the workflow actions the caller may perform.</summary>
    public IReadOnlyList<KnowledgeWorkflowAction> AllowedActions { get; set; } = [];

    /// <summary>Gets a value indicating whether the validity date has passed (expired articles are hidden from readers).</summary>
    public bool IsExpired => ValidTo is { } validTo && validTo < DateOnly.FromDateTime(DateTime.UtcNow);
}
