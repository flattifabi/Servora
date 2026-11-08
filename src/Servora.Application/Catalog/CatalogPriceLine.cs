namespace Servora.Application.Catalog;

/// <summary>One line of a price quote (base price or the price impact of a chosen option), per unit.</summary>
/// <param name="Label">The label, for example the item name or "32 GB RAM".</param>
/// <param name="Amount">The amount per unit, billed like the item.</param>
public sealed record CatalogPriceLine(string Label, decimal Amount);
