using Servora.Application.Common;

namespace Servora.Application.Catalog;

/// <summary>Catalog builder use cases: categories, items and variables. Every method requires <c>catalog.manage</c>.</summary>
public interface ICatalogAdminService
{
    /// <summary>Lists all categories with item counts.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The categories.</returns>
    Task<Result<IReadOnlyList<CatalogCategoryModel>>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a category.</summary>
    /// <param name="model">The category.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The category identifier.</returns>
    Task<Result<Guid>> SaveCategoryAsync(CatalogCategoryModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes an empty category.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists catalog items.</summary>
    /// <param name="categoryId">An optional category filter.</param>
    /// <param name="page">Paging, sorting (<c>Name</c>, <c>Category</c>, <c>Updated</c>) and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of items.</returns>
    Task<Result<PagedResult<CatalogItemSummary>>> ListItemsAsync(Guid? categoryId, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets key figures of the catalog.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The statistics.</returns>
    Task<Result<CatalogAdminStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets an item with its variables.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The item.</returns>
    Task<Result<CatalogItemModel>> GetItemAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates an item.</summary>
    /// <param name="model">The item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier.</returns>
    Task<Result<Guid>> CreateItemAsync(CatalogItemModel model, CancellationToken cancellationToken = default);

    /// <summary>Updates an item (not its variables).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="model">The item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateItemAsync(Guid id, CatalogItemModel model, CancellationToken cancellationToken = default);

    /// <summary>Copies an item including its variables (the copy is inactive).</summary>
    /// <param name="id">The identifier of the item to copy.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifier of the copy.</returns>
    Task<Result<Guid>> DuplicateItemAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes an item that was never ordered (deactivate ordered items instead).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a variable of an item.</summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="model">The variable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The variable identifier.</returns>
    Task<Result<Guid>> SaveVariableAsync(Guid itemId, CatalogVariableModel model, CancellationToken cancellationToken = default);

    /// <summary>Deletes a variable.</summary>
    /// <param name="variableId">The variable identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteVariableAsync(Guid variableId, CancellationToken cancellationToken = default);

    /// <summary>Moves a variable one position up (negative direction) or down (positive direction).</summary>
    /// <param name="variableId">The variable identifier.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> MoveVariableAsync(Guid variableId, int direction, CancellationToken cancellationToken = default);
}
