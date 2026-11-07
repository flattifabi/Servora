namespace Servora.Application.Portal;

/// <summary>A knowledge category with published articles, as shown on the portal knowledge page.</summary>
/// <param name="Id">The category identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="KnowledgeBaseName">The knowledge base name.</param>
/// <param name="ArticleCount">The number of published articles.</param>
public sealed record PortalKnowledgeCategory(Guid Id, string Name, string? Description, string Icon, string KnowledgeBaseName, int ArticleCount);
