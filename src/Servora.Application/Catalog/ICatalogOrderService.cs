using Servora.Application.Common;

namespace Servora.Application.Catalog;

/// <summary>
/// Catalog browsing and ordering used by the self-service portal (T-11) and fulfillers. Browsing requires
/// <c>portal.access</c>, <c>request.read</c> or <c>catalog.manage</c>; ordering requires <c>portal.access</c> or
/// <c>request.write</c> (ordering for another user requires <c>request.write</c>).
/// </summary>
public interface ICatalogOrderService
{
    /// <summary>Gets the active categories that contain active items.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The categories with the number of orderable items.</returns>
    Task<Result<IReadOnlyList<CatalogCategoryModel>>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets orderable items.</summary>
    /// <param name="categoryId">An optional category filter.</param>
    /// <param name="search">An optional search term (name and short description).</param>
    /// <param name="take">The maximum number of items (clamped to 1..200).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items.</returns>
    Task<Result<IReadOnlyList<CatalogItemCard>>> GetItemsAsync(Guid? categoryId, string? search, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets the order form of an active item.</summary>
    /// <param name="catalogItemId">The catalog item identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The order form.</returns>
    Task<Result<CatalogOrderForm>> GetOrderFormAsync(Guid catalogItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Orders a catalog item: validates the answers, creates the request (REQ) and requested item (RITM), requests the
    /// approval when required, otherwise creates the fulfilment tasks immediately.
    /// </summary>
    /// <param name="catalogItemId">The catalog item identifier.</param>
    /// <param name="variables">The answers keyed by variable name.</param>
    /// <param name="requestedForId">The user the item is for (defaults to the current user).</param>
    /// <param name="quantity">The quantity (1..100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The confirmation.</returns>
    Task<Result<OrderConfirmation>> OrderAsync(Guid catalogItemId, IReadOnlyDictionary<string, string?> variables, Guid? requestedForId = null, int quantity = 1, CancellationToken cancellationToken = default);
}
