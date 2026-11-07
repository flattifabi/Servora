namespace Servora.Application.Knowledge;

/// <summary>A ranked knowledge search result.</summary>
/// <param name="Id">The article identifier.</param>
/// <param name="Number">The article number.</param>
/// <param name="Title">The title.</param>
/// <param name="Summary">The summary.</param>
/// <param name="Snippet">A plain-text excerpt around the first matching term.</param>
/// <param name="Score">The relevance score (higher is better).</param>
/// <param name="KnowledgeBaseName">The knowledge base name.</param>
/// <param name="CategoryName">The category name.</param>
/// <param name="ViewCount">The number of views.</param>
/// <param name="HelpfulCount">The number of helpful votes.</param>
/// <param name="PublishedAt">The publication time.</param>
public sealed record KnowledgeSearchHit(
    Guid Id,
    string Number,
    string Title,
    string? Summary,
    string Snippet,
    double Score,
    string KnowledgeBaseName,
    string? CategoryName,
    int ViewCount,
    int HelpfulCount,
    DateTimeOffset? PublishedAt);
