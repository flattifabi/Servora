using System.Globalization;

namespace Servora.Web.Components.Portal;

/// <summary>Display formatting helpers of the self-service portal.</summary>
public static class PortalFormat
{
    /// <summary>Formats a catalog price.</summary>
    /// <param name="price">The price.</param>
    /// <returns><c>Free</c> or the amount with two decimals.</returns>
    public static string Price(decimal price) => price == 0 ? "Free" : price.ToString("#,##0.00", CultureInfo.InvariantCulture);

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
}
