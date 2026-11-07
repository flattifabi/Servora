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

    /// <summary>Gets the price.</summary>
    public decimal Price { get; init; }

    /// <summary>Gets the delivery time in days.</summary>
    public int DeliveryDays { get; init; }

    /// <summary>Gets a value indicating whether an approval is required.</summary>
    public bool ApprovalRequired { get; init; }
}
