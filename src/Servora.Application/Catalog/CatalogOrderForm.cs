namespace Servora.Application.Catalog;

/// <summary>Everything needed to render the order form of a catalog item.</summary>
/// <param name="Item">The item card.</param>
/// <param name="Description">The long description (plain text).</param>
/// <param name="Variables">The variables in display order.</param>
public sealed record CatalogOrderForm(CatalogItemCard Item, string? Description, IReadOnlyList<CatalogVariableDefinition> Variables);
