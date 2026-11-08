namespace Servora.Application.Catalog;

/// <summary>Everything needed to render the order page of a catalog item.</summary>
/// <param name="Item">The item card.</param>
/// <param name="Description">The long description (plain text).</param>
/// <param name="Variables">The variables in display order.</param>
public sealed record CatalogOrderForm(CatalogItemCard Item, string? Description, IReadOnlyList<CatalogVariableDefinition> Variables)
{
    /// <summary>Gets the order form sections in display order (variables reference them by identifier).</summary>
    public IReadOnlyList<CatalogSectionModel> Sections { get; init; } = [];

    /// <summary>Gets the highlight facts.</summary>
    public IReadOnlyList<CatalogHighlight> Highlights { get; init; } = [];

    /// <summary>Gets the "Details &amp; equipment" content (Markdown; render it with the safe Markdown renderer).</summary>
    public string? DetailsMarkdown { get; init; }

    /// <summary>Gets the "Delivery &amp; support" content (Markdown; render it with the safe Markdown renderer).</summary>
    public string? DeliveryMarkdown { get; init; }

    /// <summary>Gets the price settings used for the live price calculation.</summary>
    public CatalogPricing Pricing { get; init; } = new(string.Empty, 0m, Servora.Domain.Catalog.CatalogBillingType.OneTime, null, 0m);

    /// <summary>
    /// Gets who will approve an order of the current user when the item requires approval, for example the manager's
    /// name or the approval group (<see langword="null"/> when unknown or not required).
    /// </summary>
    public string? ApproverName { get; init; }

    /// <summary>Gets a value indicating whether the current user saved the item as a favourite.</summary>
    public bool IsFavorite { get; init; }

    /// <summary>Gets a value indicating whether the current user may order for other people.</summary>
    public bool CanOrderOnBehalf { get; init; }

    /// <summary>Gets the current user's display name.</summary>
    public string? RequesterName { get; init; }

    /// <summary>Gets the current user's department.</summary>
    public string? RequesterDepartment { get; init; }
}
