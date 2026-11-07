namespace Servora.Web.Components.UI.Charts;

/// <summary>Categorical chart colors matching the design system.</summary>
public static class ChartPalette
{
    /// <summary>Gets the ordered categorical colors.</summary>
    public static IReadOnlyList<string> Colors { get; } =
        ["#7c6cff", "#22d3ee", "#f43f5e", "#22c55e", "#f59e0b", "#a855f7", "#3b82f6", "#14b8a6", "#ec4899", "#64748b"];

    /// <summary>Gets the color for a series index.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The color.</returns>
    public static string At(int index) => Colors[Math.Abs(index) % Colors.Count];
}
