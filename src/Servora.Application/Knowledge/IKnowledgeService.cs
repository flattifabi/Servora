using Servora.Application.Common;
using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>
/// Knowledge Management use cases: knowledge bases and categories, article authoring with versions, the review / publish
/// workflow, reader views and helpfulness votes, and links to configuration items. Every method enforces permissions.
/// Readers (<c>knowledge.read</c>) only see published, valid articles; authors (<c>knowledge.write</c>) and reviewers
/// (<c>knowledge.publish</c>) also see drafts, articles in review and retired articles.
/// </summary>
public interface IKnowledgeService
{
    /// <summary>Gets the knowledge home page data.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data.</returns>
    Task<Result<KnowledgeHomeData>> GetHomeAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists knowledge bases.</summary>
    /// <param name="includeInactive">Whether inactive bases are included (requires <c>knowledge.manage</c>).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bases.</returns>
    Task<Result<IReadOnlyList<KnowledgeBaseDto>>> GetBasesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Lists categories.</summary>
    /// <param name="knowledgeBaseId">An optional knowledge base filter.</param>
    /// <param name="includeInactive">Whether inactive categories are included (requires <c>knowledge.manage</c>).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The categories.</returns>
    Task<Result<IReadOnlyList<KnowledgeCategoryDto>>> GetCategoriesAsync(Guid? knowledgeBaseId = null, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a knowledge base (<c>knowledge.manage</c>).</summary>
    /// <param name="id">The id to update, or <see langword="null"/> to create.</param>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveBaseAsync(Guid? id, KnowledgeBaseInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes an empty knowledge base (<c>knowledge.manage</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteBaseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a category (<c>knowledge.manage</c>).</summary>
    /// <param name="id">The id to update, or <see langword="null"/> to create.</param>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> SaveCategoryAsync(Guid? id, KnowledgeCategoryInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category; its articles become uncategorised (<c>knowledge.manage</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists articles (paged).</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<KnowledgeArticleSummary>>> ListAsync(KnowledgeArticleQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets an article with rendered HTML and the caller's capabilities.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The article.</returns>
    Task<Result<KnowledgeArticleDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a draft article (<c>knowledge.write</c>).</summary>
    /// <param name="input">The content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> CreateAsync(KnowledgeArticleInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates the content of a draft or an article in review (<c>knowledge.write</c>). Content changes create a version.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="input">The content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, KnowledgeArticleInput input, CancellationToken cancellationToken = default);

    /// <summary>Performs a workflow action (submit, publish, reject, retire, revise).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="action">The action.</param>
    /// <param name="comment">An optional comment (reviewer feedback).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> TransitionAsync(Guid id, KnowledgeWorkflowAction action, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes an article and its versions (<c>knowledge.manage</c>).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Counts a reader view of a published article.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RecordViewAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Records or changes the current user's helpfulness vote.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="helpful">Whether the article was helpful.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> VoteAsync(Guid id, bool helpful, CancellationToken cancellationToken = default);

    /// <summary>Lists the version snapshots of an article (authors and reviewers).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The versions, newest first.</returns>
    Task<Result<IReadOnlyList<KnowledgeVersionDto>>> GetVersionsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the Markdown body of a version snapshot (authors and reviewers).</summary>
    /// <param name="articleId">The article.</param>
    /// <param name="versionId">The snapshot.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The Markdown.</returns>
    Task<Result<string>> GetVersionBodyAsync(Guid articleId, Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>Links a configuration item to an article (<c>knowledge.write</c>).</summary>
    /// <param name="id">The article.</param>
    /// <param name="configurationItemId">The CI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> AddRelatedCiAsync(Guid id, Guid configurationItemId, CancellationToken cancellationToken = default);

    /// <summary>Removes a configuration item link (<c>knowledge.write</c>).</summary>
    /// <param name="id">The article.</param>
    /// <param name="configurationItemId">The CI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RemoveRelatedCiAsync(Guid id, Guid configurationItemId, CancellationToken cancellationToken = default);

    /// <summary>Lists the articles linked to a configuration item that the caller may see.</summary>
    /// <param name="configurationItemId">The CI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The articles (max 50).</returns>
    Task<Result<IReadOnlyList<KnowledgeArticleSummary>>> GetForConfigurationItemAsync(Guid configurationItemId, CancellationToken cancellationToken = default);
}
