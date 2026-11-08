using System.Globalization;
using Servora.Application.Catalog;
using Servora.Domain.Catalog;

namespace Servora.Web.Components.Portal;

/// <summary>Display formatting helpers of the self-service portal.</summary>
public static class PortalFormat
{
    /// <summary>The text shown for items without a price.</summary>
    public const string NoCost = "No additional cost";

    /// <summary>Formats a catalog price (legacy compact form).</summary>
    /// <param name="price">The price.</param>
    /// <returns><c>Free</c> or the amount with two decimals.</returns>
    public static string Price(decimal price) => price == 0 ? "Free" : price.ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>Formats an amount with the configured currency, for example <c>€39.00</c> or <c>CHF 39.00</c>.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency code (EUR, USD, GBP, CHF).</param>
    /// <returns>The formatted amount.</returns>
    public static string Money(decimal amount, string? currency)
    {
        var sign = amount < 0 ? "−" : string.Empty;
        var value = Math.Abs(amount).ToString("#,##0.00", CultureInfo.InvariantCulture);
        return (currency ?? "EUR") switch
        {
            "USD" => $"{sign}${value}",
            "GBP" => $"{sign}£{value}",
            "CHF" => $"{sign}CHF {value}",
            _ => $"{sign}€{value}",
        };
    }

    /// <summary>Formats a price impact, for example <c>+ €8.00 / month</c>.</summary>
    /// <param name="delta">The price impact.</param>
    /// <param name="monthly">Whether the item is billed monthly.</param>
    /// <param name="currency">The currency code.</param>
    /// <returns>The text.</returns>
    public static string Delta(decimal delta, bool monthly, string? currency) =>
        $"{(delta < 0 ? "−" : "+")} {Money(Math.Abs(delta), currency)}{(monthly ? " / month" : string.Empty)}";

    /// <summary>Gets the price label of a catalog card, for example <c>from €39.00 / month</c> or <c>No additional cost</c>.</summary>
    /// <param name="item">The item.</param>
    /// <param name="currency">The currency code.</param>
    /// <returns>The label.</returns>
    public static string PriceLabel(CatalogItemCard item, string? currency)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Price == 0 && !item.HasPriceOptions)
        {
            return NoCost;
        }

        var amount = Money(item.Price, currency) + (item.BillingType == CatalogBillingType.Monthly ? " / month" : string.Empty);
        return item.HasPriceOptions ? $"from {amount}" : amount;
    }

    /// <summary>Formats a total with its billing period.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="monthly">Whether it is billed monthly.</param>
    /// <param name="currency">The currency code.</param>
    /// <returns>The text, or <see cref="NoCost"/> for zero.</returns>
    public static string Period(decimal amount, bool monthly, string? currency) =>
        amount == 0 ? NoCost : Money(amount, currency) + (monthly ? " / month" : string.Empty);

    /// <summary>Gets the delivery text of a catalog card.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The text, for example <c>3–5 working days</c> or <c>Available immediately</c>.</returns>
    public static string DeliveryLabel(CatalogItemCard item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return !string.IsNullOrWhiteSpace(item.DeliveryText) ? item.DeliveryText : WorkingDays(item.DeliveryDays);
    }

    /// <summary>Formats a delivery time in working days.</summary>
    /// <param name="days">The delivery time in days.</param>
    /// <returns>The text.</returns>
    public static string WorkingDays(int days) => days switch
    {
        <= 0 => "Available immediately",
        1 => "1 working day",
        _ => $"{days} working days",
    };

    /// <summary>Formats a delivery time.</summary>
    /// <param name="days">The delivery time in days.</param>
    /// <returns>The text, for example <c>3 days</c>.</returns>
    public static string Delivery(int days) => days switch
    {
        0 => "Same day",
        1 => "1 day",
        _ => $"{days} days",
    };

    /// <summary>Formats a file size.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The human readable size.</returns>
    public static string FileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB",
    };

    /// <summary>Formats a UTC timestamp as a short absolute date.</summary>
    /// <param name="value">The timestamp.</param>
    /// <returns>The text, for example <c>7 Nov 2026</c>.</returns>
    public static string Date(DateTimeOffset? value) =>
        value is { } v ? v.UtcDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "—";

    /// <summary>Gets a greeting for the time of day (UTC based).</summary>
    /// <param name="now">The current time.</param>
    /// <returns>The greeting.</returns>
    public static string Greeting(DateTimeOffset now) => now.ToLocalTime().Hour switch
    {
        < 5 => "Good evening",
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening",
    };

    /// <summary>Gets the first name part of a display name.</summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The first word.</returns>
    public static string FirstName(string? displayName)
    {
        var name = (displayName ?? string.Empty).Trim();
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? name[..space] : name;
    }

    /// <summary>Gets up to two initials of a display name.</summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The initials, for example <c>LM</c>.</returns>
    public static string Initials(string? displayName)
    {
        var parts = (displayName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant(),
        };
    }

    /// <summary>Maps a status tone to the CSS modifier of a portal status pill.</summary>
    /// <param name="tone">The tone (success, warning, danger, info, neutral).</param>
    /// <returns>The modifier.</returns>
    public static string Tone(string? tone) => tone switch
    {
        "success" => "success",
        "warning" => "warning",
        "danger" => "danger",
        "neutral" => "neutral",
        _ => "info",
    };
}
