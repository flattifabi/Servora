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
    /// <param name="search">An optional search term (name, subtitle and short description).</param>
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
    /// Calculates the price of a configured item from the stored catalog definition (the same calculation
    /// <see cref="OrderAsync"/> uses).
    /// </summary>
    /// <param name="catalogItemId">The catalog item identifier.</param>
    /// <param name="variables">The answers keyed by variable name.</param>
    /// <param name="quantity">The quantity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The quote.</returns>
    Task<Result<CatalogPriceQuote>> QuoteAsync(Guid catalogItemId, IReadOnlyDictionary<string, string?> variables, int quantity = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Orders a catalog item: validates the answers, calculates the price server-side, creates the request (REQ) and
    /// requested item (RITM), requests the approval when required, otherwise creates the fulfilment tasks immediately.
    /// </summary>
    /// <param name="catalogItemId">The catalog item identifier.</param>
    /// <param name="variables">The answers keyed by variable name.</param>
    /// <param name="requestedForId">The user the item is for (defaults to the current user).</param>
    /// <param name="quantity">The quantity (1..100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The confirmation.</returns>
    Task<Result<OrderConfirmation>> OrderAsync(Guid catalogItemId, IReadOnlyDictionary<string, string?> variables, Guid? requestedForId = null, int quantity = 1, CancellationToken cancellationToken = default);

    /// <summary>Gets the identifiers of the current user's favourite items (users only).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The item identifiers.</returns>
    Task<Result<IReadOnlyList<Guid>>> GetFavoriteIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds an active item to or removes it from the current user's favourites (users only).</summary>
    /// <param name="catalogItemId">The catalog item identifier.</param>
    /// <param name="favorite"><see langword="true"/> to add, <see langword="false"/> to remove.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetFavoriteAsync(Guid catalogItemId, bool favorite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the image of a catalog item. Images of inactive items are only available to catalog managers; unknown
    /// identifiers are reported as not found.
    /// </summary>
    /// <param name="imageId">The image identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image.</returns>
    Task<Result<CatalogImage>> OpenImageAsync(Guid imageId, CancellationToken cancellationToken = default);
}
