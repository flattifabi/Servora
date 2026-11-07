namespace Servora.Application.Common;

/// <summary>A page of items plus the total number of matching items.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on the current page.</param>
/// <param name="TotalCount">The total number of items matching the query.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The page size.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    /// <summary>Gets the number of pages.</summary>
    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Creates an empty page.</summary>
    /// <returns>An empty result.</returns>
    public static PagedResult<T> Empty() => new([], 0, 1, 25);
}
