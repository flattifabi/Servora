using Servora.Application.Common;
using Servora.Domain.Knowledge;

namespace Servora.Application.Knowledge;

/// <summary>Filter for article lists.</summary>
public sealed record KnowledgeArticleQuery
{
    /// <summary>Gets the state filter.</summary>
    public KnowledgeArticleState? State { get; init; }

    /// <summary>Gets the knowledge base filter.</summary>
    public Guid? KnowledgeBaseId { get; init; }

    /// <summary>Gets the category filter.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>Gets a value indicating whether only articles authored by the current user are returned.</summary>
    public bool MineOnly { get; init; }

    /// <summary>Gets paging, sorting (<c>Number</c>, <c>Title</c>, <c>State</c>, <c>ViewCount</c>, <c>UpdatedAt</c>) and search.</summary>
    public PageRequest Page { get; init; } = new();
}
