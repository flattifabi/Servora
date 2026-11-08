using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>The price of a configured catalog item as computed by <see cref="CatalogPriceCalculator"/>.</summary>
public sealed record CatalogPriceQuote
{
    /// <summary>Gets how the prices are billed.</summary>
    public CatalogBillingType BillingType { get; init; }

    /// <summary>Gets the contract term of monthly billed items.</summary>
    public int? TermMonths { get; init; }

    /// <summary>Gets the quantity.</summary>
    public int Quantity { get; init; } = 1;

    /// <summary>Gets the base price line followed by the price impacts of the chosen options (per unit).</summary>
    public IReadOnlyList<CatalogPriceLine> Lines { get; init; } = [];

    /// <summary>Gets the labels of the chosen configuration options (for summaries).</summary>
    public IReadOnlyList<string> SelectedOptions { get; init; } = [];

    /// <summary>Gets the price per unit including option impacts (monthly price for monthly billed items).</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Gets the one-time costs per unit (monthly billed items only).</summary>
    public decimal UnitOneTimePrice { get; init; }

    /// <summary>Gets the total price (unit price × quantity).</summary>
    public decimal Total => UnitPrice * Quantity;

    /// <summary>Gets the total one-time costs (one-time costs × quantity).</summary>
    public decimal OneTimeTotal => UnitOneTimePrice * Quantity;

    /// <summary>Gets a value indicating whether the price is billed monthly.</summary>
    public bool IsMonthly => BillingType == CatalogBillingType.Monthly;
}
