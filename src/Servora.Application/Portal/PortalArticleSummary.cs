namespace Servora.Application.Portal;

/// <summary>A published knowledge article as listed in the portal.</summary>
public sealed record PortalArticleSummary
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the article number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the summary.</summary>
    public string? Summary { get; init; }

    /// <summary>Gets a plain-text excerpt around the first search match (search results only).</summary>
    public string? Snippet { get; init; }

    /// <summary>Gets the knowledge base name.</summary>
    public string KnowledgeBaseName { get; init; } = string.Empty;

    /// <summary>Gets the category name.</summary>
    public string? CategoryName { get; init; }

    /// <summary>Gets the number of views.</summary>
    public int ViewCount { get; init; }

    /// <summary>Gets the share of helpful votes in percent (<see langword="null"/> without votes).</summary>
    public int? HelpfulPercent { get; init; }

    /// <summary>Gets the UTC publication time.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Gets the relative portal route.</summary>
    public string Route => $"/portal/knowledge/{Id}";
}
