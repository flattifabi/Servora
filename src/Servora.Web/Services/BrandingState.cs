using System.Globalization;
using System.Text.RegularExpressions;
using Servora.Application.Configuration;

namespace Servora.Web.Services;

/// <summary>
/// Process-wide snapshot of the instance branding for synchronous use while rendering (page titles, the document
/// head). Layouts refresh it from <see cref="IBrandingService"/> whenever they load, the branding panel after a save,
/// and the host once at startup.
/// </summary>
/// <remarks>
/// The design system derives its whole palette from one brand colour (<c>--brand</c> / <c>--primary</c>), so the
/// document head only embeds the validated accent and the text colour that is readable on top of it
/// (<c>--primary-contrast</c>); see <see cref="ThemeStyle"/>.
/// </remarks>
public sealed partial class BrandingState
{
    /// <summary>Dark text used on light accent colours (matches the design system ink).</summary>
    public const string DarkContrastColor = "#10201a";

    /// <summary>Light text used on dark accent colours.</summary>
    public const string LightContrastColor = "#ffffff";

    private BrandingInfo _current = BrandingInfo.Default;

    /// <summary>Gets the last loaded branding.</summary>
    public BrandingInfo Current => Volatile.Read(ref _current);

    /// <summary>Gets the product name used in page titles and the navigation.</summary>
    public string ProductName => Current.ProductName;

    /// <summary>
    /// Gets the accent color when it is a valid <c>#rrggbb</c> value that differs from the design system default,
    /// otherwise <see langword="null"/> (safe to embed in a style element).
    /// </summary>
    public string? CustomAccentColor => NormalizeCustomAccent(Current.AccentColor);

    /// <summary>
    /// Gets the CSS rule that applies the custom accent to the whole product (light and dark theme derive their
    /// tints from it), or <see langword="null"/> when the default palette applies. Only validated values are used.
    /// </summary>
    public string? ThemeStyle => CustomAccentColor is { } accent
        ? $":root {{ --brand: {accent}; --primary: {accent}; --primary-contrast: {ContrastColorFor(accent)}; }}"
        : null;

    /// <summary>Loads the current branding and updates the snapshot.</summary>
    /// <param name="branding">The branding service of the current scope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current branding.</returns>
    public async Task<BrandingInfo> RefreshAsync(IBrandingService branding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branding);
        var info = await branding.GetAsync(cancellationToken);
        Volatile.Write(ref _current, info);
        return info;
    }

    /// <summary>Loads the branding once at startup (in its own scope).</summary>
    /// <param name="services">The root service provider.</param>
    /// <returns>A task that completes when the snapshot was loaded.</returns>
    public async Task WarmUpAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        await RefreshAsync(scope.ServiceProvider.GetRequiredService<IBrandingService>());
    }

    /// <summary>
    /// Returns the validated, lower-case accent when it is a custom colour, otherwise <see langword="null"/> (invalid
    /// values, the design system default and the retired defaults of earlier versions).
    /// </summary>
    /// <param name="accent">The configured accent colour.</param>
    /// <returns>The accent to embed, or <see langword="null"/>.</returns>
    public static string? NormalizeCustomAccent(string? accent)
    {
        if (accent is null || !HexColor().IsMatch(accent))
        {
            return null;
        }

        var normalized = accent.ToLowerInvariant();
        return string.Equals(normalized, CoreSettingDefinitionProvider.DefaultAccentColor, StringComparison.OrdinalIgnoreCase)
            || CoreSettingDefinitionProvider.IsLegacyDefaultAccentColor(normalized)
            ? null
            : normalized;
    }

    /// <summary>
    /// Picks the text colour (white or dark ink) with the higher WCAG contrast ratio on the given background colour.
    /// </summary>
    /// <param name="hexColor">A <c>#rrggbb</c> colour.</param>
    /// <returns><see cref="LightContrastColor"/> or <see cref="DarkContrastColor"/>.</returns>
    public static string ContrastColorFor(string hexColor)
    {
        if (!HexColor().IsMatch(hexColor ?? string.Empty))
        {
            return LightContrastColor;
        }

        var background = RelativeLuminance(hexColor!);
        var withWhite = 1.05 / (background + 0.05);
        var withDark = (background + 0.05) / (RelativeLuminance(DarkContrastColor) + 0.05);
        return withWhite >= withDark ? LightContrastColor : DarkContrastColor;
    }

    /// <summary>Computes the WCAG 2.x relative luminance of a <c>#rrggbb</c> colour.</summary>
    /// <param name="hexColor">The colour.</param>
    /// <returns>The luminance between 0 (black) and 1 (white).</returns>
    public static double RelativeLuminance(string hexColor)
    {
        ArgumentNullException.ThrowIfNull(hexColor);
        static double Channel(string hex, int offset)
        {
            var value = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(hexColor, 1)) + (0.7152 * Channel(hexColor, 3)) + (0.0722 * Channel(hexColor, 5));
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
