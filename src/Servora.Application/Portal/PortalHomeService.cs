using Microsoft.EntityFrameworkCore;
using Servora.Application.Approvals;
using Servora.Application.Catalog;
using Servora.Application.Common;
using Servora.Domain.Catalog;
using Servora.Domain.Itsm;

namespace Servora.Application.Portal;

/// <summary>
/// Default <see cref="IPortalHomeService"/> implementation. Catalog data comes from the catalog module's
/// <see cref="ICatalogOrderService"/>, approval counts from <see cref="IApprovalService"/>.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="catalog">The catalog ordering service.</param>
/// <param name="approvals">The approval service.</param>
/// <param name="knowledge">The portal knowledge service.</param>
/// <param name="announcements">The announcement service.</param>
/// <param name="clock">The clock.</param>
public sealed class PortalHomeService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    ICatalogOrderService catalog,
    IApprovalService approvals,
    IPortalKnowledgeService knowledge,
    IPortalAnnouncementService announcements,
    IClock clock) : IPortalHomeService
{
    private const int PopularWindowDays = 90;

    /// <inheritdoc />
    public async Task<Result<PortalHomeData>> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalHomeData>.Forbidden();
        }

        int openIncidents, awaitingConfirmation, openRequests;
        IReadOnlyList<PortalTicketSummary> recent;
        Dictionary<Guid, int> popularity;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var incidents = PortalScope.OwnIncidents(db, me);
            openIncidents = await incidents.CountAsync(i => i.Active, cancellationToken);
            awaitingConfirmation = await incidents.CountAsync(i => i.State == IncidentState.Resolved, cancellationToken);
            openRequests = await PortalScope.OwnRequests(db, me).CountAsync(r => r.State == RequestState.Submitted || r.State == RequestState.InProgress, cancellationToken);
            (recent, _) = await PortalTicketReader.ReadAsync(db, me, PortalTicketFilter.Open, null, null, 0, 6, cancellationToken);

            var since = clock.UtcNow.AddDays(-PopularWindowDays);
            popularity = await db.Set<RequestItem>().AsNoTracking()
                .Where(i => i.OpenedAt >= since)
                .GroupBy(i => i.CatalogItemId)
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(50)
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        }

        var items = await catalog.GetItemsAsync(null, null, 200, cancellationToken);
        var popularItems = (items.Value ?? [])
            .Select((item, index) => (Item: item, Index: index))
            .OrderByDescending(x => popularity.GetValueOrDefault(x.Item.Id))
            .ThenBy(x => x.Index)
            .Take(6)
            .Select(x => x.Item)
            .ToList();

        var knowledgeHome = await knowledge.GetHomeAsync(cancellationToken);
        var live = await announcements.GetLiveAsync(cancellationToken);
        var pending = await approvals.CountPendingForUserAsync(me, cancellationToken);

        return Result<PortalHomeData>.Success(new PortalHomeData
        {
            OpenIncidents = openIncidents,
            AwaitingConfirmation = awaitingConfirmation,
            OpenRequests = openRequests,
            PendingApprovals = pending ?? 0,
            RecentItems = recent,
            PopularItems = popularItems,
            PopularArticles = knowledgeHome.Value?.Popular.Take(4).ToList() ?? [],
            Announcements = live.Value ?? [],
        });
    }

    /// <inheritdoc />
    public async Task<Result<PortalSearchResults>> SearchAsync(string? query, int take = 6, CancellationToken cancellationToken = default)
    {
        if (await PortalScope.PortalUserAsync(currentUser, cancellationToken) is not { } me)
        {
            return Result<PortalSearchResults>.Forbidden();
        }

        var term = PortalScope.Term(query);
        if (term is null)
        {
            return Result<PortalSearchResults>.Success(new PortalSearchResults());
        }

        take = Math.Clamp(take, 1, 20);
        var articles = await knowledge.SearchAsync(term, take, cancellationToken);
        var catalogItems = await catalog.GetItemsAsync(null, term, take, cancellationToken);
        IReadOnlyList<PortalTicketSummary> mine;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            (mine, _) = await PortalTicketReader.ReadAsync(db, me, PortalTicketFilter.All, null, term, 0, take, cancellationToken);
        }

        return Result<PortalSearchResults>.Success(new PortalSearchResults
        {
            Query = query!.Trim(),
            Articles = articles.Value ?? [],
            CatalogItems = catalogItems.Value ?? [],
            MyItems = mine,
        });
    }
}
