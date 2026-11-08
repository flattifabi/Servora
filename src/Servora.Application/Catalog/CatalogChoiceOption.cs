using System.Globalization;

namespace Servora.Application.Catalog;

/// <summary>
/// One option of a choice variable with its optional price impact and description. Options are stored one per line in
/// <c>CatalogVariable.Options</c>, either as a plain label (the original format, no price impact) or as
/// <c>Label | price delta | description</c>. A line whose second segment is not a number is treated as a plain label,
/// so existing options that contain a <c>|</c> keep working unchanged.
/// </summary>
/// <param name="Label">The label (also the stored answer).</param>
/// <param name="PriceDelta">The price impact (billed like the item; 0 = included in the base price).</param>
/// <param name="Description">An optional one-line description shown on option cards.</param>
public sealed record CatalogChoiceOption(string Label, decimal PriceDelta = 0m, string? Description = null)
{
    /// <summary>The separator between label, price delta and description.</summary>
    public const char Separator = '|';

    /// <summary>The largest absolute price impact an option may have.</summary>
    public const decimal MaxPriceDelta = 1_000_000m;

    /// <summary>Parses one option line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The option, or <see langword="null"/> for empty lines.</returns>
    public static CatalogChoiceOption? Parse(string? line)
    {
        var text = line?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var parts = text.Split(Separator);
        if (parts.Length >= 2 && TryParsePrice(parts[1], out var price))
        {
            var label = parts[0].Trim();
            if (label.Length == 0)
            {
                return null;
            }

            var description = parts.Length > 2 ? string.Join(Separator, parts.Skip(2)).Trim() : null;
            return new CatalogChoiceOption(label, price, string.IsNullOrEmpty(description) ? null : description);
        }

        return new CatalogChoiceOption(text);
    }

    /// <summary>Parses newline separated option lines into a distinct list (first occurrence of a label wins).</summary>
    /// <param name="text">The option lines.</param>
    /// <returns>The options in their original order.</returns>
    public static IReadOnlyList<CatalogChoiceOption> ParseMany(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new List<CatalogChoiceOption>();
        foreach (var line in text.Split('\n'))
        {
            if (Parse(line) is { } option && seen.Add(option.Label))
            {
                options.Add(option);
            }
        }

        return options;
    }

    /// <summary>Formats the option as a storable line (plain label when it has neither price nor description).</summary>
    /// <returns>The line.</returns>
    public string ToLine()
    {
        if (PriceDelta == 0m && string.IsNullOrEmpty(Description))
        {
            return Label;
        }

        var price = PriceDelta.ToString("+0.00;-0.00;0", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(Description) ? $"{Label} {Separator} {price}" : $"{Label} {Separator} {price} {Separator} {Description}";
    }

    private static bool TryParsePrice(string text, out decimal price)
    {
        var value = text.Trim().Replace("€", string.Empty, StringComparison.Ordinal).Trim();
        if (value.Length == 0)
        {
            price = 0m;
            return false;
        }

        if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price))
        {
            price = decimal.Round(price, 2);
            return true;
        }

        return false;
    }
}
