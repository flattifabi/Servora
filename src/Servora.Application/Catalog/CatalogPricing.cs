using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>The price settings of a catalog item that <see cref="CatalogPriceCalculator"/> needs.</summary>
/// <param name="ItemName">The item name (label of the base price line).</param>
/// <param name="BasePrice">The base price per unit.</param>
/// <param name="BillingType">How the price is billed.</param>
/// <param name="TermMonths">The contract term of monthly billed items.</param>
/// <param name="OneTimePrice">The one-time costs per unit of monthly billed items.</param>
public sealed record CatalogPricing(string ItemName, decimal BasePrice, CatalogBillingType BillingType, int? TermMonths, decimal OneTimePrice)
{
    /// <summary>Creates the pricing of a catalog item entity.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The pricing.</returns>
    public static CatalogPricing From(CatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new CatalogPricing(item.Name, item.Price, item.BillingType, item.TermMonths, item.BillingType == CatalogBillingType.Monthly ? item.OneTimePrice : 0m);
    }
}
