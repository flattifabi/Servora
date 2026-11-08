namespace Servora.Infrastructure.Catalog;

/// <summary>A compact definition of a showcase catalog item seeded by <see cref="CatalogSeeder"/>.</summary>
/// <param name="Name">The item name.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="Subtitle">The subtitle (model or edition).</param>
/// <param name="ShortDescription">The short description.</param>
/// <param name="Price">The base price.</param>
/// <param name="Monthly">Whether the price is billed monthly.</param>
/// <param name="TermMonths">The term of monthly billed items.</param>
/// <param name="DeliveryDays">The delivery time in days.</param>
/// <param name="DeliveryText">The delivery text.</param>
/// <param name="Code">The service identifier.</param>
/// <param name="Order">The sort order within the category.</param>
internal sealed record CatalogShowcaseItem(
    string Name,
    string Icon,
    string? Subtitle,
    string ShortDescription,
    decimal Price,
    bool Monthly,
    int? TermMonths,
    int DeliveryDays,
    string? DeliveryText,
    string Code,
    int Order);
