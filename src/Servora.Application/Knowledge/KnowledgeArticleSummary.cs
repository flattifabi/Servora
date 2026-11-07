using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>A knowledge article row for lists and cards.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Number">The number (KB…).</param>
/// <param name="Title">The title.</param>
/// <param name="Summary">The summary.</param>
/// <param name="State">The workflow state.</param>
/// <param name="KnowledgeBaseId">The knowledge base.</param>
/// <param name="KnowledgeBaseName">The knowledge base name.</param>
/// <param name="CategoryId">The category.</param>
/// <param name="CategoryName">The category name.</param>
/// <param name="AuthorName">The author's display name.</param>
/// <param name="ViewCount">The number of views.</param>
/// <param name="HelpfulCount">The number of helpful votes.</param>
/// <param name="NotHelpfulCount">The number of not helpful votes.</param>
/// <param name="UpdatedAt">The last update time.</param>
/// <param name="PublishedAt">The publication time.</param>
/// <param name="ArticleVersion">The content version.</param>
public sealed record KnowledgeArticleSummary(
    Guid Id,
    string Number,
    string Title,
    string? Summary,
    KnowledgeArticleState State,
    Guid KnowledgeBaseId,
    string KnowledgeBaseName,
    Guid? CategoryId,
    string? CategoryName,
    string? AuthorName,
    int ViewCount,
    int HelpfulCount,
    int NotHelpfulCount,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    int ArticleVersion)
{
    /// <summary>Gets the helpfulness ratio in percent, or <see langword="null"/> without votes.</summary>
    public int? HelpfulPercent => HelpfulCount + NotHelpfulCount == 0 ? null : (int)Math.Round(HelpfulCount * 100.0 / (HelpfulCount + NotHelpfulCount));
}
