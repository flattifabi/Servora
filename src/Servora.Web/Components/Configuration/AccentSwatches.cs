using Servora.Application.Configuration;

namespace Servora.Web.Components.Configuration;

/// <summary>
/// Curated accent colours offered by the branding panel and the setup wizard. All of them are deep enough for white
/// text and for the derived brand ink (sidebar, portal header); any other <c>#rrggbb</c> value can still be entered.
/// </summary>
public static class AccentSwatches
{
    /// <summary>Gets the preset accent colours, the design system default (forest green) first.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        CoreSettingDefinitionProvider.DefaultAccentColor, // forest green
        "#0e6b5c", // teal
        "#1d4f91", // royal blue
        "#0f5f7a", // petrol
        "#4b3f8f", // indigo
        "#7a2e44", // burgundy
        "#8a4a17", // copper
        "#2f3e46", // slate
    ];
}
