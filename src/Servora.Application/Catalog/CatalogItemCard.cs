using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>An orderable catalog item as shown on catalog browsing pages (portal).</summary>
public sealed record CatalogItemCard
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the short description.</summary>
    public string? ShortDescription { get; init; }

    /// <summary>Gets the icon.</summary>
    public string Icon { get; init; } = "package";

    /// <summary>Gets the category.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>Gets the category name.</summary>
    public string? CategoryName { get; init; }

    /// <summary>Gets the base price (monthly price for monthly billed items).</summary>
    public decimal Price { get; init; }

    /// <summary>Gets how the price is billed.</summary>
    public CatalogBillingType BillingType { get; init; }

    /// <summary>Gets the contract term in months of monthly billed items.</summary>
    public int? TermMonths { get; init; }

    /// <summary>Gets the one-time costs of monthly billed items.</summary>
    public decimal OneTimePrice { get; init; }

    /// <summary>Gets a value indicating whether choice options or accessories can change the price ("from" prices).</summary>
    public bool HasPriceOptions { get; init; }

    /// <summary>Gets the delivery time in days.</summary>
    public int DeliveryDays { get; init; }

    /// <summary>Gets the delivery text that replaces the computed one, if any.</summary>
    public string? DeliveryText { get; init; }

    /// <summary>Gets a value indicating whether an approval is required.</summary>
    public bool ApprovalRequired { get; init; }

    /// <summary>Gets the subtitle (for example the model).</summary>
    public string? Subtitle { get; init; }

    /// <summary>Gets the badge text.</summary>
    public string? BadgeText { get; init; }

    /// <summary>Gets the public service identifier.</summary>
    public string? Code { get; init; }

    /// <summary>Gets the relative URL of the product image, or <see langword="null"/> when the item has none.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Gets the teaser headline of the card panel.</summary>
    public string? Teaser { get; init; }

    /// <summary>Gets the line below the teaser headline.</summary>
    public string? TeaserText { get; init; }

    /// <summary>Gets a value indicating whether the item is featured on the portal home page.</summary>
    public bool IsFeatured { get; init; }

    /// <summary>Gets the order among featured items.</summary>
    public int FeaturedOrder { get; init; }

    /// <summary>Gets the sort order within the category.</summary>
    public int Order { get; init; }
}
