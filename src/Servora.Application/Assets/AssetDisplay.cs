using System.Globalization;
using Servora.Domain.Assets;

namespace Servora.Application.Assets;

/// <summary>Display helpers for asset values (labels, tones, icons, warranty status, money).</summary>
public static class AssetDisplay
{
    /// <summary>The number of days before the warranty end at which an asset counts as "expiring soon".</summary>
    public const int ExpiringDays = 90;

    /// <summary>Gets the design system tone of a lifecycle state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The tone.</returns>
    public static string StateTone(AssetState state) => state switch
    {
        AssetState.InStock => "info",
        AssetState.InUse => "success",
        AssetState.InRepair => "warning",
        AssetState.Retired => "neutral",
        AssetState.Disposed => "neutral",
        _ => "neutral",
    };

    /// <summary>Gets the label of a category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The label.</returns>
    public static string Category(AssetCategory category) => category switch
    {
        AssetCategory.Hardware => "Hardware",
        AssetCategory.Software => "Software",
        AssetCategory.License => "License",
        _ => category.ToString(),
    };

    /// <summary>Gets the icon of a category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The icon name.</returns>
    public static string CategoryIcon(AssetCategory category) => category switch
    {
        AssetCategory.Hardware => "laptop",
        AssetCategory.Software => "package",
        AssetCategory.License => "key",
        _ => "box",
    };

    /// <summary>Gets the verb shown on the button that moves an asset into the given state.</summary>
    /// <param name="target">The target state.</param>
    /// <returns>The action label.</returns>
    public static string TransitionAction(AssetState target) => target switch
    {
        AssetState.InStock => "Return to stock",
        AssetState.InUse => "Put in use",
        AssetState.InRepair => "Send to repair",
        AssetState.Retired => "Retire",
        AssetState.Disposed => "Dispose",
        _ => target.ToString(),
    };

    /// <summary>Gets the icon of the action that moves an asset into the given state.</summary>
    /// <param name="target">The target state.</param>
    /// <returns>The icon name.</returns>
    public static string TransitionIcon(AssetState target) => target switch
    {
        AssetState.InStock => "inbox",
        AssetState.InUse => "user-check",
        AssetState.InRepair => "wrench",
        AssetState.Retired => "history",
        AssetState.Disposed => "trash",
        _ => "arrow-right",
    };

    /// <summary>Classifies the warranty (or license expiry) of an asset relative to a day.</summary>
    /// <param name="warrantyEnd">The warranty end, if known.</param>
    /// <param name="today">The reference day.</param>
    /// <returns><c>none</c>, <c>expired</c>, <c>expiring</c> (within <see cref="ExpiringDays"/>) or <c>valid</c>.</returns>
    public static string WarrantyStatus(DateOnly? warrantyEnd, DateOnly today)
    {
        if (warrantyEnd is not { } end)
        {
            return "none";
        }

        if (end < today)
        {
            return "expired";
        }

        return end <= today.AddDays(ExpiringDays) ? "expiring" : "valid";
    }

    /// <summary>Gets the tone matching a <see cref="WarrantyStatus"/> value.</summary>
    /// <param name="status">The warranty status.</param>
    /// <returns>The tone.</returns>
    public static string WarrantyTone(string status) => status switch
    {
        "expired" => "danger",
        "expiring" => "warning",
        "valid" => "success",
        _ => "neutral",
    };

    /// <summary>Gets a human readable warranty description such as "Expires in 23 days".</summary>
    /// <param name="warrantyEnd">The warranty end, if known.</param>
    /// <param name="today">The reference day.</param>
    /// <returns>The description.</returns>
    public static string WarrantyText(DateOnly? warrantyEnd, DateOnly today)
    {
        if (warrantyEnd is not { } end)
        {
            return "No warranty recorded";
        }

        var days = end.DayNumber - today.DayNumber;
        return days switch
        {
            < -1 => $"Expired {-days} days ago",
            -1 => "Expired yesterday",
            0 => "Expires today",
            1 => "Expires tomorrow",
            <= ExpiringDays => $"Expires in {days} days",
            _ => $"Valid until {end.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}",
        };
    }

    /// <summary>Formats an amount with its ISO currency code, for example <c>EUR 1,299.00</c>.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The ISO currency code.</param>
    /// <param name="compact">When <see langword="true"/>, large amounts are abbreviated (k, M).</param>
    /// <returns>The formatted value.</returns>
    public static string Money(decimal amount, string currency, bool compact = false)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant() + " ";
        if (compact)
        {
            if (amount >= 1_000_000m)
            {
                return code + (amount / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture) + "M";
            }

            if (amount >= 10_000m)
            {
                return code + (amount / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "k";
            }
        }

        return code + amount.ToString("N2", CultureInfo.InvariantCulture);
    }

    /// <summary>Gets the license utilisation in percent (installs / entitlements), capped at 999.</summary>
    /// <param name="entitlements">The entitlements.</param>
    /// <param name="installs">The installations.</param>
    /// <returns>The utilisation, or <see langword="null"/> when no entitlements are recorded.</returns>
    public static int? LicenseUtilisation(int? entitlements, int? installs)
    {
        if (entitlements is not > 0)
        {
            return installs is > 0 ? 999 : null;
        }

        return (int)Math.Min(999, Math.Round((installs ?? 0) * 100.0 / entitlements.Value));
    }
}
