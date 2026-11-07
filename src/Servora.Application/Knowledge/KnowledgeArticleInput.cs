namespace Servora.Application.Knowledge;

/// <summary>Editable content of a knowledge article.</summary>
public sealed class KnowledgeArticleInput
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the summary.</summary>
    public string? Summary { get; set; }

    /// <summary>Gets or sets the Markdown body.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the knowledge base.</summary>
    public Guid KnowledgeBaseId { get; set; }

    /// <summary>Gets or sets the category.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Gets or sets the validity end date.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>Gets or sets comma separated keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Gets or sets an optional change note stored with the version snapshot.</summary>
    public string? ChangeNote { get; set; }

    /// <summary>Gets or sets the expected concurrency version (0 for new articles).</summary>
    public long Version { get; set; }
}
