namespace Servora.Web.Components.KnowledgeCmdb;

/// <summary>Splits plain text into segments so that search terms can be highlighted without rendering raw HTML.</summary>
public static class SearchHighlighter
{
    /// <summary>Splits text into matching and non-matching segments (case-insensitive).</summary>
    /// <param name="text">The plain text.</param>
    /// <param name="terms">The search terms.</param>
    /// <returns>The segments in order.</returns>
    public static IReadOnlyList<HighlightSegment> Split(string? text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var marks = new bool[text.Length];
        foreach (var term in terms.Where(t => t.Length > 0))
        {
            var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                for (var i = index; i < index + term.Length; i++)
                {
                    marks[i] = true;
                }

                index = text.IndexOf(term, index + term.Length, StringComparison.OrdinalIgnoreCase);
            }
        }

        var segments = new List<HighlightSegment>();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || marks[i] != marks[start])
            {
                segments.Add(new HighlightSegment(text[start..i], marks[start]));
                start = i;
            }
        }

        return segments;
    }
}
