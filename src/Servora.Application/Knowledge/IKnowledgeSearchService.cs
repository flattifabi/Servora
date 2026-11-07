using Servora.Application.Common;

namespace Servora.Application.Knowledge;

/// <summary>
/// Ranked full-text search over published knowledge articles. Used by the knowledge home page, the self-service portal and
/// AI assistants / MCP tools. Requires <c>knowledge.read</c>.
/// </summary>
public interface IKnowledgeSearchService
{
    /// <summary>Searches published, valid articles by title, summary, keywords and body terms.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The hits ordered by descending relevance.</returns>
    Task<Result<IReadOnlyList<KnowledgeSearchHit>>> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default);
}
