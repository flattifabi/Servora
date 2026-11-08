using System.Text.RegularExpressions;

namespace Servora.Application.Catalog;

/// <summary>A highlight fact of a catalog item (icon and short text), parsed from <c>CatalogItem.Highlights</c>.</summary>
/// <param name="Icon">The icon name of the UI icon set.</param>
/// <param name="Text">The text.</param>
public sealed partial record CatalogHighlight(string Icon, string Text)
{
    /// <summary>The icon used for lines without an icon prefix.</summary>
    public const string DefaultIcon = "check-circle";

    /// <summary>The maximum number of highlights shown.</summary>
    public const int MaxHighlights = 6;

    /// <summary>Parses highlight lines (<c>icon | text</c> or just <c>text</c>).</summary>
    /// <param name="text">The lines.</param>
    /// <returns>The highlights (at most <see cref="MaxHighlights"/>).</returns>
    public static IReadOnlyList<CatalogHighlight> ParseMany(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var result = new List<CatalogHighlight>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('|', StringComparison.Ordinal);
            var icon = separator > 0 ? line[..separator].Trim() : string.Empty;
            var label = separator > 0 ? line[(separator + 1)..].Trim() : line;
            if (label.Length == 0)
            {
                continue;
            }

            result.Add(new CatalogHighlight(IconPattern().IsMatch(icon) ? icon : DefaultIcon, label.Length > 80 ? label[..80] : label));
            if (result.Count == MaxHighlights)
            {
                break;
            }
        }

        return result;
    }

    [GeneratedRegex("^[a-z0-9-]{1,50}$")]
    private static partial Regex IconPattern();
}
