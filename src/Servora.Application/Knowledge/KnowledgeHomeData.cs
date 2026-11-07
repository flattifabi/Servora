namespace Servora.Application.Knowledge;

/// <summary>Data of the knowledge home page.</summary>
public sealed class KnowledgeHomeData
{
    /// <summary>Gets or sets the active knowledge bases.</summary>
    public IReadOnlyList<KnowledgeBaseDto> Bases { get; set; } = [];

    /// <summary>Gets or sets the active categories.</summary>
    public IReadOnlyList<KnowledgeCategoryDto> Categories { get; set; } = [];

    /// <summary>Gets or sets the most viewed published articles.</summary>
    public IReadOnlyList<KnowledgeArticleSummary> Popular { get; set; } = [];

    /// <summary>Gets or sets the most recently published articles.</summary>
    public IReadOnlyList<KnowledgeArticleSummary> Newest { get; set; } = [];

    /// <summary>Gets or sets the number of published articles.</summary>
    public int PublishedCount { get; set; }

    /// <summary>Gets or sets the total number of views of published articles.</summary>
    public int TotalViews { get; set; }

    /// <summary>Gets or sets the overall helpfulness in percent (or <see langword="null"/> without votes).</summary>
    public int? HelpfulPercent { get; set; }

    /// <summary>Gets or sets the number of drafts (only for authors, otherwise 0).</summary>
    public int DraftCount { get; set; }

    /// <summary>Gets or sets the number of articles awaiting review (only for authors, otherwise 0).</summary>
    public int ReviewCount { get; set; }
}
