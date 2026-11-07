namespace Servora.Application.Knowledge;

/// <summary>A ranked knowledge search request (published, valid articles only).</summary>
/// <param name="Query">The free-text query.</param>
/// <param name="Take">The maximum number of hits (clamped to 1..50).</param>
/// <param name="KnowledgeBaseId">An optional knowledge base filter.</param>
/// <param name="CategoryId">An optional category filter.</param>
public sealed record KnowledgeSearchRequest(string Query, int Take = 10, Guid? KnowledgeBaseId = null, Guid? CategoryId = null);
