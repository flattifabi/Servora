using System.Globalization;
using System.Text;

namespace Servora.Application.Knowledge.Markdown;

/// <summary>
/// Renders block-level Markdown: ATX headings, paragraphs, fenced code blocks, block quotes, (nested) ordered and unordered
/// lists, horizontal rules and GitHub-style tables. Inline content is delegated to <see cref="MarkdownInlineRenderer"/>.
/// One instance renders one document (it tracks heading ids).
/// </summary>
internal sealed class MarkdownBlockRenderer
{
    private const int MaxNesting = 8;
    private readonly StringBuilder _html = new();
    private readonly List<MarkdownHeading> _headings = [];
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    /// <summary>Renders a whole document.</summary>
    /// <param name="markdown">The Markdown source.</param>
    /// <returns>The rendered result.</returns>
    public MarkdownRenderResult Render(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\t", "    ", StringComparison.Ordinal).Split('\n');
        RenderBlocks(lines, _html, 0);
        return new MarkdownRenderResult(_html.ToString(), _headings);
    }

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static int LeadingSpaces(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static bool TryFence(string line, out char fenceChar, out int fenceLength, out string language)
    {
        fenceChar = '\0';
        fenceLength = 0;
        language = string.Empty;
        var indent = LeadingSpaces(line);
        if (indent > 3 || indent >= line.Length || (line[indent] != '`' && line[indent] != '~'))
        {
            return false;
        }

        var c = line[indent];
        var run = 0;
        while (indent + run < line.Length && line[indent + run] == c)
        {
            run++;
        }

        if (run < 3)
        {
            return false;
        }

        var info = line[(indent + run)..].Trim();
        if (c == '`' && info.Contains('`'))
        {
            return false;
        }

        fenceChar = c;
        fenceLength = run;
        var lang = new StringBuilder();
        foreach (var ch in info)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '+' or '#' or '_')
            {
                lang.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                break;
            }

            if (lang.Length >= 30)
            {
                break;
            }
        }

        language = lang.ToString();
        return true;
    }

    private static bool IsFenceClose(string line, char fenceChar, int fenceLength)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= fenceLength && trimmed.All(c => c == fenceChar);
    }

    private static bool TryHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        var indent = LeadingSpaces(line);
        if (indent > 3)
        {
            return false;
        }

        var i = indent;
        while (i < line.Length && line[i] == '#')
        {
            level++;
            i++;
        }

        if (level is < 1 or > 6 || (i < line.Length && line[i] != ' '))
        {
            return false;
        }

        text = line[i..].Trim();

        // Optional closing sequence of #.
        var trimmedEnd = text.TrimEnd('#');
        if (trimmedEnd.Length == 0 || trimmedEnd.EndsWith(' '))
        {
            text = trimmedEnd.Trim();
        }

        return true;
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3 || LeadingSpaces(line) > 3)
        {
            return false;
        }

        var c = trimmed[0];
        if (c is not ('-' or '*' or '_'))
        {
            return false;
        }

        var count = 0;
        foreach (var ch in trimmed)
        {
            if (ch == c)
            {
                count++;
            }
            else if (ch != ' ')
            {
                return false;
            }
        }

        return count >= 3;
    }

    private static bool IsBlockquote(string line) => LeadingSpaces(line) <= 3 && line.TrimStart().StartsWith('>');

    private static bool TryListItem(string line, out bool ordered, out int start, out int indent, out int contentIndent, out string content)
    {
        ordered = false;
        start = 1;
        content = string.Empty;
        indent = LeadingSpaces(line);
        contentIndent = 0;
        var i = indent;
        if (i >= line.Length)
        {
            return false;
        }

        if (line[i] is '-' or '*' or '+')
        {
            i++;
        }
        else
        {
            var digits = 0;
            while (i < line.Length && char.IsAsciiDigit(line[i]) && digits < 9)
            {
                i++;
                digits++;
            }

            if (digits == 0 || i >= line.Length || line[i] is not ('.' or ')'))
            {
                return false;
            }

            ordered = true;
            start = int.Parse(line[indent..i], NumberStyles.None, CultureInfo.InvariantCulture);
            i++;
        }

        if (i < line.Length && line[i] != ' ')
        {
            return false;
        }

        var spaces = 0;
        while (i + spaces < line.Length && line[i + spaces] == ' ' && spaces < 4)
        {
            spaces++;
        }

        contentIndent = i + Math.Max(1, spaces);
        content = i + spaces <= line.Length ? line[(i + spaces)..] : string.Empty;
        return true;
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('-') || !(trimmed.Contains('|') || trimmed.Contains(':')))
        {
            return false;
        }

        return trimmed.All(c => c is '|' or '-' or ':' or ' ') && SplitRow(trimmed).All(cell => cell.Trim().Trim(':').Length > 0 && cell.Trim().Trim(':').All(c => c == '-'));
    }

    private static List<string> SplitRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|') && !trimmed.EndsWith("\\|", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        var cells = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
            {
                current.Append('|');
                i++;
            }
            else if (trimmed[i] == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(trimmed[i]);
            }
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static string Dedent(string line, int amount)
    {
        var remove = Math.Min(amount, LeadingSpaces(line));
        return line[remove..];
    }

    private bool IsBlockStart(IReadOnlyList<string> lines, int index)
    {
        var line = lines[index];
        return TryFence(line, out _, out _, out _) || TryHeading(line, out _, out _) || IsHorizontalRule(line) || IsBlockquote(line)
            || (TryListItem(line, out _, out _, out var indent, out _, out var content) && indent <= 3 && content.Trim().Length > 0)
            || (index + 1 < lines.Count && line.Contains('|') && IsTableSeparator(lines[index + 1]));
    }

    private void RenderBlocks(IReadOnlyList<string> lines, StringBuilder sb, int depth)
    {
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (IsBlank(line))
            {
                i++;
                continue;
            }

            if (TryFence(line, out var fenceChar, out var fenceLength, out var language))
            {
                i = RenderFencedCode(lines, i, fenceChar, fenceLength, language, sb);
                continue;
            }

            if (TryHeading(line, out var level, out var headingText))
            {
                RenderHeading(level, headingText, sb, depth);
                i++;
                continue;
            }

            if (IsHorizontalRule(line))
            {
                sb.Append("<hr />\n");
                i++;
                continue;
            }

            if (IsBlockquote(line))
            {
                i = RenderBlockquote(lines, i, sb, depth);
                continue;
            }

            if (TryListItem(line, out _, out _, out var itemIndent, out _, out _) && itemIndent <= 3)
            {
                i = RenderList(lines, i, sb, depth);
                continue;
            }

            if (i + 1 < lines.Count && line.Contains('|') && IsTableSeparator(lines[i + 1]))
            {
                i = RenderTable(lines, i, sb, depth);
                continue;
            }

            i = RenderParagraph(lines, i, sb, depth);
        }
    }

    private static int RenderFencedCode(IReadOnlyList<string> lines, int start, char fenceChar, int fenceLength, string language, StringBuilder sb)
    {
        var indent = LeadingSpaces(lines[start]);
        var i = start + 1;
        var code = new StringBuilder();
        while (i < lines.Count && !IsFenceClose(lines[i], fenceChar, fenceLength))
        {
            code.Append(Dedent(lines[i], indent)).Append('\n');
            i++;
        }

        sb.Append("<pre><code");
        if (language.Length > 0)
        {
            sb.Append(" class=\"language-");
            MarkdownHtml.Encode(language, sb);
            sb.Append('"');
        }

        sb.Append('>');
        MarkdownHtml.Encode(code.ToString(), sb);
        sb.Append("</code></pre>\n");
        return Math.Min(lines.Count, i + 1);
    }

    private void RenderHeading(int level, string text, StringBuilder sb, int depth)
    {
        var inner = new StringBuilder();
        MarkdownInlineRenderer.Render(text, inner, depth);
        var plain = MarkdownHtml.StripTags(inner.ToString());
        var id = MarkdownHtml.Slugify(plain);
        var unique = id;
        for (var n = 2; !_ids.Add(unique); n++)
        {
            unique = $"{id}-{n.ToString(CultureInfo.InvariantCulture)}";
        }

        _headings.Add(new MarkdownHeading(level, plain, unique));
        sb.Append("<h").Append(level).Append(" id=\"").Append(unique).Append("\" tabindex=\"-1\">")
            .Append(inner).Append("</h").Append(level).Append(">\n");
    }

    private int RenderBlockquote(IReadOnlyList<string> lines, int start, StringBuilder sb, int depth)
    {
        var inner = new List<string>();
        var i = start;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (IsBlockquote(line))
            {
                var content = line.TrimStart()[1..];
                inner.Add(content.StartsWith(' ') ? content[1..] : content);
            }
            else if (!IsBlank(line) && inner.Count > 0 && !IsBlank(inner[^1]) && !IsBlockStart(lines, i))
            {
                inner.Add(line); // lazy continuation
            }
            else
            {
                break;
            }

            i++;
        }

        sb.Append("<blockquote>\n");
        if (depth >= MaxNesting)
        {
            sb.Append("<p>");
            MarkdownHtml.Encode(string.Join(' ', inner), sb);
            sb.Append("</p>\n");
        }
        else
        {
            RenderBlocks(inner, sb, depth + 1);
        }

        sb.Append("</blockquote>\n");
        return i;
    }

    private int RenderList(IReadOnlyList<string> lines, int start, StringBuilder sb, int depth)
    {
        TryListItem(lines[start], out var ordered, out var startNumber, out var baseIndent, out _, out _);
        var items = new List<List<string>>();
        var contentIndent = 0;
        var i = start;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (TryListItem(line, out var itemOrdered, out _, out var indent, out var itemContentIndent, out var content)
                && indent <= baseIndent + 1 && !IsHorizontalRule(line))
            {
                if (itemOrdered != ordered)
                {
                    break;
                }

                items.Add([content]);
                contentIndent = itemContentIndent;
                i++;
                continue;
            }

            if (IsBlank(line))
            {
                var next = i + 1;
                while (next < lines.Count && IsBlank(lines[next]))
                {
                    next++;
                }

                var continues = next < lines.Count && (LeadingSpaces(lines[next]) >= contentIndent
                    || (TryListItem(lines[next], out var nextOrdered, out _, out var nextIndent, out _, out _) && nextIndent <= baseIndent + 1 && nextOrdered == ordered));
                if (!continues)
                {
                    break;
                }

                items[^1].Add(string.Empty);
                i++;
                continue;
            }

            if (LeadingSpaces(line) > baseIndent)
            {
                items[^1].Add(Dedent(line, contentIndent));
                i++;
                continue;
            }

            if (!IsBlank(items[^1][^1]) && !IsBlockStart(lines, i))
            {
                items[^1].Add(line.Trim()); // lazy continuation
                i++;
                continue;
            }

            break;
        }

        if (ordered)
        {
            sb.Append("<ol");
            if (startNumber != 1)
            {
                sb.Append(" start=\"").Append(startNumber.ToString(CultureInfo.InvariantCulture)).Append('"');
            }

            sb.Append(">\n");
        }
        else
        {
            sb.Append("<ul>\n");
        }

        foreach (var item in items)
        {
            sb.Append("<li>");
            RenderListItem(item, sb, depth);
            sb.Append("</li>\n");
        }

        sb.Append(ordered ? "</ol>\n" : "</ul>\n");
        return i;
    }

    private void RenderListItem(List<string> item, StringBuilder sb, int depth)
    {
        while (item.Count > 0 && IsBlank(item[^1]))
        {
            item.RemoveAt(item.Count - 1);
        }

        if (depth >= MaxNesting)
        {
            MarkdownHtml.Encode(string.Join(' ', item), sb);
            return;
        }

        if (item.Any(IsBlank))
        {
            RenderBlocks(item, sb, depth + 1);
            return;
        }

        // Tight item: leading text inline, the rest (for example a nested list) as blocks.
        var textLines = new List<string>();
        var index = 0;
        while (index < item.Count && (index == 0 || !IsBlockStart(item, index)))
        {
            textLines.Add(item[index]);
            index++;
        }

        if (textLines.Count == 1 && TryListItem(textLines[0], out _, out _, out _, out _, out _) && textLines[0].Trim().Length > 1)
        {
            // Item that directly starts with a nested list marker ("- - x").
            RenderBlocks(item, sb, depth + 1);
            return;
        }

        MarkdownInlineRenderer.Render(string.Join('\n', textLines).Trim(), sb, depth);
        if (index < item.Count)
        {
            sb.Append('\n');
            RenderBlocks(item.Skip(index).ToList(), sb, depth + 1);
        }
    }

    private int RenderTable(IReadOnlyList<string> lines, int start, StringBuilder sb, int depth)
    {
        var header = SplitRow(lines[start]);
        var alignments = SplitRow(lines[start + 1]).Select(cell =>
        {
            var c = cell.Trim();
            var left = c.StartsWith(':');
            var right = c.EndsWith(':');
            return left && right ? "center" : right ? "right" : left ? "left" : null;
        }).ToList();

        sb.Append("<div class=\"md-table\"><table>\n<thead><tr>");
        for (var c = 0; c < header.Count; c++)
        {
            AppendCell("th", header[c], c < alignments.Count ? alignments[c] : null, sb, depth);
        }

        sb.Append("</tr></thead>\n<tbody>\n");
        var i = start + 2;
        while (i < lines.Count && !IsBlank(lines[i]) && lines[i].Contains('|'))
        {
            var cells = SplitRow(lines[i]);
            sb.Append("<tr>");
            for (var c = 0; c < header.Count; c++)
            {
                AppendCell("td", c < cells.Count ? cells[c] : string.Empty, c < alignments.Count ? alignments[c] : null, sb, depth);
            }

            sb.Append("</tr>\n");
            i++;
        }

        sb.Append("</tbody>\n</table></div>\n");
        return i;
    }

    private static void AppendCell(string tag, string content, string? alignment, StringBuilder sb, int depth)
    {
        sb.Append('<').Append(tag);
        if (alignment is not null)
        {
            sb.Append(" class=\"align-").Append(alignment).Append('"');
        }

        sb.Append('>');
        MarkdownInlineRenderer.Render(content, sb, depth);
        sb.Append("</").Append(tag).Append('>');
    }

    private int RenderParagraph(IReadOnlyList<string> lines, int start, StringBuilder sb, int depth)
    {
        var text = new List<string> { lines[start].TrimStart() };
        var i = start + 1;
        while (i < lines.Count && !IsBlank(lines[i]) && !IsBlockStart(lines, i))
        {
            text.Add(lines[i].TrimStart());
            i++;
        }

        sb.Append("<p>");
        MarkdownInlineRenderer.Render(string.Join('\n', text).TrimEnd(), sb, depth);
        sb.Append("</p>\n");
        return i;
    }
}
