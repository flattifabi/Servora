using System.Text.RegularExpressions;
using Servora.Application.Configuration;

namespace Servora.Web.Services;

/// <summary>
/// Process-wide snapshot of the instance branding for synchronous use while rendering (page titles, the document
/// head). Layouts refresh it from <see cref="IBrandingService"/> whenever they load, the branding panel after a save,
/// and the host once at startup.
/// </summary>
public sealed partial class BrandingState
{
    private BrandingInfo _current = BrandingInfo.Default;

    /// <summary>Gets the last loaded branding.</summary>
    public BrandingInfo Current => Volatile.Read(ref _current);

    /// <summary>Gets the product name used in page titles and the navigation.</summary>
    public string ProductName => Current.ProductName;

    /// <summary>
    /// Gets the accent color when it is a valid <c>#rrggbb</c> value that differs from the design system default,
    /// otherwise <see langword="null"/> (safe to embed in a style element).
    /// </summary>
    public string? CustomAccentColor
    {
        get
        {
            var accent = Current.AccentColor;
            return HexColor().IsMatch(accent ?? string.Empty) && !string.Equals(accent, CoreSettingDefinitionProvider.DefaultAccentColor, StringComparison.OrdinalIgnoreCase)
                ? accent!.ToLowerInvariant()
                : null;
        }
    }

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

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
