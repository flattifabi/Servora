namespace Servora.Application.Knowledge;

/// <summary>A knowledge category with its number of published articles.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="KnowledgeBaseId">The owning knowledge base.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="SortOrder">The display order.</param>
/// <param name="IsActive">Whether the category is active.</param>
/// <param name="ArticleCount">The number of published articles.</param>
/// <param name="Version">The concurrency version.</param>
public sealed record KnowledgeCategoryDto(Guid Id, Guid KnowledgeBaseId, string Name, string? Description, string Icon, int SortOrder, bool IsActive, int ArticleCount, long Version);
