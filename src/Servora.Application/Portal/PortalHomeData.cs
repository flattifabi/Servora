using Servora.Application.Catalog;

namespace Servora.Application.Portal;

/// <summary>Everything the portal home page shows for the current user.</summary>
public sealed record PortalHomeData
{
    /// <summary>Gets the number of the user's open requests.</summary>
    public int OpenRequests { get; init; }

    /// <summary>Gets the number of the user's open incidents.</summary>
    public int OpenIncidents { get; init; }

    /// <summary>Gets the number of approvals waiting for the user's decision.</summary>
    public int PendingApprovals { get; init; }

    /// <summary>Gets the number of resolved incidents waiting for the user's confirmation.</summary>
    public int AwaitingConfirmation { get; init; }

    /// <summary>Gets the user's most recently updated open items.</summary>
    public IReadOnlyList<PortalTicketSummary> RecentItems { get; init; } = [];

    /// <summary>Gets the most frequently ordered catalog items.</summary>
    public IReadOnlyList<CatalogItemCard> PopularItems { get; init; } = [];

    /// <summary>Gets the most viewed knowledge articles.</summary>
    public IReadOnlyList<PortalArticleSummary> PopularArticles { get; init; } = [];

    /// <summary>Gets the live announcements.</summary>
    public IReadOnlyList<PortalAnnouncementView> Announcements { get; init; } = [];

    /// <summary>Gets the active catalog categories that contain orderable items (with real item counts).</summary>
    public IReadOnlyList<CatalogCategoryModel> Categories { get; init; } = [];

    /// <summary>Gets the number of orderable catalog items.</summary>
    public int TotalServices { get; init; }

    /// <summary>
    /// Gets the items of the "frequently used" section: the featured items (in their configured order) or, when none are
    /// featured, the most ordered items.
    /// </summary>
    public IReadOnlyList<CatalogItemCard> SpotlightItems { get; init; } = [];

    /// <summary>Gets the most ordered catalog item of the last 90 days (<see langword="null"/> when nothing was ordered).</summary>
    public Guid? MostOrderedItemId { get; init; }

    /// <summary>Gets the user's most recently updated items regardless of their state (home table).</summary>
    public IReadOnlyList<PortalTicketSummary> LatestItems { get; init; } = [];

    /// <summary>Gets the identifiers of the user's favourite catalog items.</summary>
    public IReadOnlyList<Guid> FavoriteIds { get; init; } = [];
}
