namespace Servora.Application.Portal;

/// <summary>Query of the current user's items.</summary>
/// <param name="Filter">The state filter.</param>
/// <param name="Kind">An optional kind filter (<see langword="null"/> = incidents and requests).</param>
/// <param name="Search">An optional search term (number or title).</param>
/// <param name="Page">The 1-based page.</param>
/// <param name="PageSize">The page size (clamped to 1..50).</param>
public sealed record PortalTicketQuery(PortalTicketFilter Filter = PortalTicketFilter.Open, PortalTicketKind? Kind = null, string? Search = null, int Page = 1, int PageSize = 20);
