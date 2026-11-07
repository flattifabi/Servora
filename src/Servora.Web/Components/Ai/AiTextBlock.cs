namespace Servora.Web.Components.Ai;

/// <summary>A block of assistant text parsed from the supported Markdown subset.</summary>
/// <param name="Kind">The block kind: <c>p</c>, <c>h</c>, <c>ul</c>, <c>ol</c>, <c>code</c> or <c>quote</c>.</param>
/// <param name="Lines">The lines (list items for lists).</param>
/// <param name="Level">The heading level for <c>h</c> blocks.</param>
public sealed record AiTextBlock(string Kind, IReadOnlyList<string> Lines, int Level = 0)
{
    /// <summary>Parses text into blocks: fenced code, headings, bullet / numbered lists, quotes and paragraphs.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The blocks.</returns>
    public static IReadOnlyList<AiTextBlock> Parse(string? text)
    {
        var blocks = new List<AiTextBlock>();
        if (string.IsNullOrEmpty(text))
        {
            return blocks;
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var buffer = new List<string>();
        string? kind = null;

        void Flush()
        {
            if (kind is not null && buffer.Count > 0)
            {
                blocks.Add(new AiTextBlock(kind, buffer.ToList()));
            }

            buffer.Clear();
            kind = null;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                var code = new List<string>();
                for (i++; i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal); i++)
                {
                    code.Add(lines[i]);
                }

                blocks.Add(new AiTextBlock("code", code));
                continue;
            }

            if (trimmed.Length == 0)
            {
                Flush();
                continue;
            }

            var heading = trimmed.TakeWhile(c => c == '#').Count();
            if (heading is > 0 and <= 4 && trimmed.Length > heading && trimmed[heading] == ' ')
            {
                Flush();
                blocks.Add(new AiTextBlock("h", [trimmed[(heading + 1)..]], heading));
                continue;
            }

            string next;
            string content;
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed.StartsWith("• ", StringComparison.Ordinal))
            {
                (next, content) = ("ul", trimmed[2..]);
            }
            else if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed.IndexOf(". ", StringComparison.Ordinal) is > 0 and < 4 and var dot && trimmed[..dot].All(char.IsDigit))
            {
                (next, content) = ("ol", trimmed[(dot + 2)..]);
            }
            else if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                (next, content) = ("quote", trimmed[2..]);
            }
            else
            {
                (next, content) = ("p", trimmed);
            }

            if (kind != next)
            {
                Flush();
                kind = next;
            }

            buffer.Add(content);
        }

        Flush();
        return blocks;
    }
}
