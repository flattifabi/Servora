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
}
