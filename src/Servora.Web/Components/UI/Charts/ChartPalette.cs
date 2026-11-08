namespace Servora.Web.Components.UI.Charts;

/// <summary>Categorical chart colors matching the design system.</summary>
public static class ChartPalette
{
    /// <summary>
    /// Gets the ordered categorical colors: the brand colour first (follows the configured accent), then calm,
    /// clearly distinguishable hues that read well on light and dark surfaces.
    /// </summary>
    public static IReadOnlyList<string> Colors { get; } =
        ["var(--primary)", "#3d7cc9", "#d4943a", "#2a9d8f", "#c75b6b", "#7d6bc2", "#8a9a3b", "#64748b", "#3fa7d6", "#a0714f"];

    /// <summary>Gets the color for a series index.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The color.</returns>
    public static string At(int index) => Colors[Math.Abs(index) % Colors.Count];
}
