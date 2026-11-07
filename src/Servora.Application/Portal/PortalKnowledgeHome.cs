namespace Servora.Application.Portal;

/// <summary>Data of the portal knowledge page.</summary>
public sealed record PortalKnowledgeHome
{
    /// <summary>Gets the categories that contain published articles.</summary>
    public IReadOnlyList<PortalKnowledgeCategory> Categories { get; init; } = [];

    /// <summary>Gets the most viewed articles.</summary>
    public IReadOnlyList<PortalArticleSummary> Popular { get; init; } = [];

    /// <summary>Gets the most recently published articles.</summary>
    public IReadOnlyList<PortalArticleSummary> Newest { get; init; } = [];

    /// <summary>Gets the number of published articles.</summary>
    public int ArticleCount { get; init; }
}
