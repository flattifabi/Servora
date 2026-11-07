using Servora.Application.Common;

namespace Servora.Application.Portal;

/// <summary>
/// Read-only knowledge access for the portal. Requires <c>portal.access</c> (or <c>knowledge.read</c>) and only ever
/// returns <b>published</b> articles of active knowledge bases whose "valid to" date has not passed – drafts, articles in
/// review and retired articles are invisible, regardless of the user's other permissions.
/// </summary>
public interface IPortalKnowledgeService
{
    /// <summary>Gets the knowledge landing page data (categories, popular and new articles).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data.</returns>
    Task<Result<PortalKnowledgeHome>> GetHomeAsync(CancellationToken cancellationToken = default);

    /// <summary>Searches published articles (ranked by relevance).</summary>
    /// <param name="query">The free-text query.</param>
    /// <param name="take">The maximum number of hits (1..50).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The hits, best first.</returns>
    Task<Result<IReadOnlyList<PortalArticleSummary>>> SearchAsync(string? query, int take = 10, CancellationToken cancellationToken = default);

    /// <summary>Lists the published articles of a category, most viewed first.</summary>
    /// <param name="categoryId">The category identifier.</param>
    /// <param name="take">The maximum number of articles (1..100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The articles.</returns>
    Task<Result<IReadOnlyList<PortalArticleSummary>>> ListByCategoryAsync(Guid categoryId, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets a published article rendered for reading.</summary>
    /// <param name="id">The article identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The article.</returns>
    Task<Result<PortalArticle>> GetArticleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Counts a view of a published article.</summary>
    /// <param name="id">The article identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RecordViewAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Records the current user's helpful / not helpful vote (one vote per user; changing it updates the counters).</summary>
    /// <param name="id">The article identifier.</param>
    /// <param name="helpful">Whether the article was helpful.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> VoteAsync(Guid id, bool helpful, CancellationToken cancellationToken = default);
}
