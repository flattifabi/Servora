using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>
/// Computes the price of a configured catalog item from the item's base price and the price impacts of the chosen
/// options (choice options and checked yes / no questions). The portal shows the result live, and
/// <see cref="ICatalogOrderService.OrderAsync"/> computes it again from the stored catalog definition when the order is
/// placed – prices sent by a client are never used.
/// </summary>
public static class CatalogPriceCalculator
{
    /// <summary>The well-known variable name of the cost centre question (shown separately in summaries).</summary>
    public const string CostCenterVariable = "cost_center";

    private static readonly string[] s_trueValues = ["true", "yes", "on", "1"];

    /// <summary>Calculates a quote.</summary>
    /// <param name="pricing">The item's price settings.</param>
    /// <param name="definitions">The item's variables.</param>
    /// <param name="answers">The answers keyed by variable name (raw or normalized); missing answers use the default value.</param>
    /// <param name="quantity">The quantity (values below 1 count as 1).</param>
    /// <returns>The quote.</returns>
    public static CatalogPriceQuote Calculate(CatalogPricing pricing, IReadOnlyList<CatalogVariableDefinition> definitions, IReadOnlyDictionary<string, string?>? answers, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(definitions);
        answers ??= new Dictionary<string, string?>();

        var lines = new List<CatalogPriceLine> { new(pricing.ItemName, decimal.Round(pricing.BasePrice, 2)) };
        var selected = new List<string>();
        foreach (var definition in definitions.OrderBy(d => d.Order))
        {
            var raw = answers.TryGetValue(definition.Name, out var provided) ? provided : definition.DefaultValue;
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            switch (definition.Type)
            {
                case CatalogVariableType.Choice:
                    var option = definition.Choices.FirstOrDefault(c => string.Equals(c.Label, value, StringComparison.OrdinalIgnoreCase));
                    if (option is null)
                    {
                        break;
                    }

                    if (!string.Equals(definition.Name, CostCenterVariable, StringComparison.Ordinal))
                    {
                        selected.Add(option.Label);
                    }

                    if (option.PriceDelta != 0m)
                    {
                        lines.Add(new CatalogPriceLine(option.Label, option.PriceDelta));
                    }

                    break;

                case CatalogVariableType.Boolean when definition.PriceDelta is { } delta && delta != 0m:
                    if (s_trueValues.Contains(value.ToLowerInvariant()))
                    {
                        selected.Add(definition.Label);
                        lines.Add(new CatalogPriceLine(definition.Label, delta));
                    }

                    break;
            }
        }

        var unit = decimal.Round(lines.Sum(l => l.Amount), 2);
        return new CatalogPriceQuote
        {
            BillingType = pricing.BillingType,
            TermMonths = pricing.BillingType == CatalogBillingType.Monthly ? pricing.TermMonths : null,
            Quantity = Math.Max(1, quantity),
            Lines = lines,
            SelectedOptions = selected,
            UnitPrice = Math.Max(0m, unit),
            UnitOneTimePrice = pricing.BillingType == CatalogBillingType.Monthly ? Math.Max(0m, decimal.Round(pricing.OneTimePrice, 2)) : 0m,
        };
    }
}
