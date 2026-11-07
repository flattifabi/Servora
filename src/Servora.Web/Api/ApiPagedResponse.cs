namespace Servora.Web.Api;

/// <summary>Standard envelope of paged REST responses.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items of the page.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The page size.</param>
/// <param name="TotalCount">The total number of matching items.</param>
/// <param name="PageCount">The number of pages.</param>
public sealed record ApiPagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int PageCount);
