namespace Servora.Application.Knowledge;

/// <summary>A knowledge base with its number of published articles.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="IsActive">Whether the knowledge base is active.</param>
/// <param name="SortOrder">The display order.</param>
/// <param name="ArticleCount">The number of published articles.</param>
/// <param name="Version">The concurrency version.</param>
public sealed record KnowledgeBaseDto(Guid Id, string Name, string? Description, string Icon, bool IsActive, int SortOrder, int ArticleCount, long Version);
