using Microsoft.EntityFrameworkCore;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Domain.Catalog;
using Servora.Domain.Itsm;

namespace Servora.Application.Portal;

/// <summary>
/// Reads the current user's incidents and requests as one list (shared by the "my items" page, the portal home page and
/// the hero search). Both sources are bounded to the requested window and merged in memory.
/// </summary>
internal static class PortalTicketReader
{
    /// <summary>The maximum number of rows read per source (page window).</summary>
    public const int MaxWindow = 1000;

    /// <summary>Reads a page of the user's items.</summary>
    /// <param name="db">The context.</param>
    /// <param name="userId">The portal user.</param>
    /// <param name="filter">The state filter.</param>
    /// <param name="kind">An optional kind filter.</param>
    /// <param name="term">A normalized search term (number or title).</param>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="take">The number of items to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items (most recently updated first) and the total count.</returns>
    public static async Task<(IReadOnlyList<PortalTicketSummary> Items, int Total)> ReadAsync(
        IAppDbContext db, Guid userId, PortalTicketFilter filter, PortalTicketKind? kind, string? term, int skip, int take, CancellationToken cancellationToken)
    {
        var window = Math.Clamp(skip + take, 1, MaxWindow);
        var rows = new List<PortalTicketSummary>();
        var total = 0;

        if (kind is null or PortalTicketKind.Incident)
        {
            var query = PortalScope.OwnIncidents(db, userId);
            query = filter switch
            {
                PortalTicketFilter.Open => query.Where(i => i.Active),
                PortalTicketFilter.Closed => query.Where(i => !i.Active),
                _ => query,
            };
            if (term is not null)
            {
                query = query.Where(i => i.Number.ToLower().Contains(term) || i.ShortDescription.ToLower().Contains(term));
            }

            total += await query.CountAsync(cancellationToken);
            var incidents = await query.OrderByDescending(i => i.UpdatedAt).Take(window)
                .Select(i => new { i.Id, i.Number, i.ShortDescription, i.State, i.OnHoldReason, i.CallerId, i.OpenedAt, i.UpdatedAt })
                .ToListAsync(cancellationToken);
            var callers = await ReferenceNameResolver.UsersAsync(db, incidents.Where(i => i.CallerId != userId).Select(i => i.CallerId), cancellationToken);
            rows.AddRange(incidents.Select(i => new PortalTicketSummary
            {
                Kind = PortalTicketKind.Incident,
                Id = i.Id,
                Number = i.Number,
                Title = i.ShortDescription,
                Status = PortalStatusMapper.ForIncident(i.State, i.OnHoldReason),
                Icon = "alert-circle",
                OnBehalfOfName = i.CallerId != userId ? callers.NameOf(i.CallerId) : null,
                OpenedAt = i.OpenedAt,
                UpdatedAt = i.UpdatedAt,
            }));
        }

        if (kind is null or PortalTicketKind.Request)
        {
            var query = PortalScope.OwnRequests(db, userId);
            query = filter switch
            {
                PortalTicketFilter.Open => query.Where(r => r.State == RequestState.Submitted || r.State == RequestState.InProgress),
                PortalTicketFilter.Closed => query.Where(r => r.State != RequestState.Submitted && r.State != RequestState.InProgress),
                _ => query,
            };
            if (term is not null)
            {
                query = query.Where(r => r.Number.ToLower().Contains(term) || r.ShortDescription.ToLower().Contains(term));
            }

            total += await query.CountAsync(cancellationToken);
            var requests = await query.OrderByDescending(r => r.UpdatedAt).Take(window)
                .Select(r => new { r.Id, r.Number, r.ShortDescription, r.State, r.RequestedForId, r.OpenedAt, r.UpdatedAt })
                .ToListAsync(cancellationToken);
            var ids = requests.Select(r => r.Id).ToList();
            var items = await db.Set<RequestItem>().AsNoTracking().Where(ri => ids.Contains(ri.RequestId))
                .OrderBy(ri => ri.Number)
                .Select(ri => new { ri.RequestId, ri.CatalogItemId, ri.State, ri.UnitPrice, ri.Quantity, ri.BillingType })
                .Take(window * 5)
                .ToListAsync(cancellationToken);
            var catalogIds = items.Select(i => i.CatalogItemId).Distinct().ToList();
            var icons = await db.Set<CatalogItem>().AsNoTracking().Where(c => catalogIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Icon })
                .ToDictionaryAsync(c => c.Id, c => c.Icon, cancellationToken);
            var recipients = await ReferenceNameResolver.UsersAsync(db, requests.Where(r => r.RequestedForId != userId).Select(r => r.RequestedForId), cancellationToken);
            rows.AddRange(requests.Select(r =>
            {
                var own = items.Where(i => i.RequestId == r.Id).ToList();
                return new PortalTicketSummary
                {
                    Kind = PortalTicketKind.Request,
                    Id = r.Id,
                    Number = r.Number,
                    Title = r.ShortDescription,
                    Status = PortalStatusMapper.ForRequest(r.State, own.Any(i => i.State == RequestItemState.PendingApproval)),
                    Icon = own.Count > 0 ? icons.GetValueOrDefault(own[0].CatalogItemId) ?? "shopping-cart" : "shopping-cart",
                    OnBehalfOfName = r.RequestedForId != userId ? recipients.NameOf(r.RequestedForId) : null,
                    OpenedAt = r.OpenedAt,
                    UpdatedAt = r.UpdatedAt,
                    Price = own.Sum(i => i.UnitPrice * i.Quantity),
                    IsMonthly = own.Any(i => i.BillingType == CatalogBillingType.Monthly),
                };
            }));
        }

        var page = rows.OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.OpenedAt).Skip(skip).Take(take).ToList();
        return (page, total);
    }
}
