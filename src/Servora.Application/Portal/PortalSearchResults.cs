using Servora.Application.Catalog;

namespace Servora.Application.Portal;

/// <summary>Results of the portal hero search across knowledge, the service catalog and the user's own items.</summary>
public sealed record PortalSearchResults
{
    /// <summary>Gets the normalized query.</summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>Gets the matching published knowledge articles, best match first.</summary>
    public IReadOnlyList<PortalArticleSummary> Articles { get; init; } = [];

    /// <summary>Gets the matching orderable catalog items.</summary>
    public IReadOnlyList<CatalogItemCard> CatalogItems { get; init; } = [];

    /// <summary>Gets the matching items of the current user (by number or title).</summary>
    public IReadOnlyList<PortalTicketSummary> MyItems { get; init; } = [];

    /// <summary>Gets a value indicating whether nothing matched.</summary>
    public bool IsEmpty => Articles.Count == 0 && CatalogItems.Count == 0 && MyItems.Count == 0;
}
