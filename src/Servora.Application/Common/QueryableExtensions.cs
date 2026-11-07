using Microsoft.EntityFrameworkCore;

namespace Servora.Application.Common;

/// <summary>Helpers for executing bounded, paged queries.</summary>
public static class QueryableExtensions
{
    /// <summary>Executes the query for one page and counts the total number of matches.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="query">The (already filtered and ordered) query.</param>
    /// <param name="request">The page request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken = default)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, total, request.Page, request.PageSize);
    }
}
