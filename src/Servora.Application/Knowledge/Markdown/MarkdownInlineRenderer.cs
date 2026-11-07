using System.Text;

namespace Servora.Application.Knowledge.Markdown;

/// <summary>
/// Renders inline Markdown (code spans, emphasis, strong, strikethrough, links, autolinks, line breaks). Every character that is
/// not recognised markup is HTML-encoded; raw HTML is never passed through.
/// </summary>
internal static class MarkdownInlineRenderer
{
    private const int MaxDepth = 12;
    private const int MaxCloserSearches = 64;
    private const int MaxLabelLength = 1000;
    private const string EscapableCharacters = "\\`*_{}[]()#+-.!|~<>\"'";

    /// <summary>Renders inline Markdown and appends safe HTML.</summary>
    /// <param name="text">The raw inline text (may contain newlines).</param>
    /// <param name="sb">The output.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <param name="allowLinks">Whether links may be created (false inside link labels).</param>
    public static void Render(string text, StringBuilder sb, int depth = 0, bool allowLinks = true)
    {
        if (depth > MaxDepth)
        {
            MarkdownHtml.Encode(text, sb);
            return;
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            switch (c)
            {
                case '\\' when i + 1 < text.Length:
                    if (text[i + 1] == '\n')
                    {
                        sb.Append("<br />\n");
                        i += 2;
                        continue;
                    }

                    if (EscapableCharacters.Contains(text[i + 1]))
                    {
                        MarkdownHtml.Encode(text[i + 1], sb);
                        i += 2;
                        continue;
                    }

                    break;
                case '`':
                    i = RenderCodeSpan(text, i, sb);
                    continue;
                case '!' when allowLinks && i + 1 < text.Length && text[i + 1] == '[':
                    if (TryRenderLink(text, i + 1, sb, depth, isImage: true, out var imageEnd))
                    {
                        i = imageEnd;
                        continue;
                    }

                    break;
                case '[' when allowLinks:
                    if (TryRenderLink(text, i, sb, depth, isImage: false, out var linkEnd))
                    {
                        i = linkEnd;
                        continue;
                    }

                    break;
                case '<' when allowLinks:
                    if (TryRenderAutolink(text, i, sb, out var autoEnd))
                    {
                        i = autoEnd;
                        continue;
                    }

                    break;
                case 'h' or 'H' when allowLinks && (i == 0 || !char.IsLetterOrDigit(text[i - 1])):
                    if (TryRenderBareUrl(text, i, sb, out var bareEnd))
                    {
                        i = bareEnd;
                        continue;
                    }

                    break;
                case '*' or '_' or '~':
                    if (TryRenderEmphasis(text, i, sb, depth, allowLinks, out var emphasisEnd))
                    {
                        i = emphasisEnd;
                        continue;
                    }

                    // Not emphasis: emit the whole delimiter run literally so that it is not re-interpreted.
                    var run = 1;
                    while (i + run < text.Length && text[i + run] == c)
                    {
                        run++;
                    }

                    sb.Append(c, run);
                    i += run;
                    continue;
                case '\n':
                    if (i >= 2 && text[i - 1] == ' ' && text[i - 2] == ' ')
                    {
                        while (sb.Length > 0 && sb[^1] == ' ')
                        {
                            sb.Length--;
                        }

                        sb.Append("<br />\n");
                    }
                    else
                    {
                        sb.Append('\n');
                    }

                    i++;
                    continue;
            }

            MarkdownHtml.Encode(c, sb);
            i++;
        }
    }

    private static int RenderCodeSpan(string text, int start, StringBuilder sb)
    {
        var run = 1;
        while (start + run < text.Length && text[start + run] == '`')
        {
            run++;
        }

        var fence = new string('`', run);
        var close = text.IndexOf(fence, start + run, StringComparison.Ordinal);
        if (close < 0)
        {
            sb.Append(fence);
            return start + run;
        }

        var content = text[(start + run)..close].Replace('\n', ' ');
        if (content.Length >= 2 && content[0] == ' ' && content[^1] == ' ' && content.Trim().Length > 0)
        {
            content = content[1..^1];
        }

        sb.Append("<code>");
        MarkdownHtml.Encode(content, sb);
        sb.Append("</code>");
        return close + run;
    }

    private static bool TryRenderEmphasis(string text, int start, StringBuilder sb, int depth, bool allowLinks, out int end)
    {
        end = start;
        var c = text[start];
        var doubled = start + 1 < text.Length && text[start + 1] == c;
        if (c == '~' && !doubled)
        {
            return false;
        }

        var length = doubled ? 2 : 1;
        var contentStart = start + length;
        if (contentStart >= text.Length || char.IsWhiteSpace(text[contentStart]))
        {
            return false;
        }

        // Intra-word underscores (snake_case) never start emphasis.
        if (c == '_' && start > 0 && char.IsLetterOrDigit(text[start - 1]))
        {
            return false;
        }

        var delimiter = new string(c, length);
        var searchFrom = contentStart;
        for (var attempt = 0; attempt < MaxCloserSearches; attempt++)
        {
            var close = text.IndexOf(delimiter, searchFrom, StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }

            var valid = close > contentStart && !char.IsWhiteSpace(text[close - 1]);
            if (valid && !doubled && close + 1 < text.Length && text[close + 1] == c)
            {
                // A single delimiter must not close on the first half of a double delimiter: skip the pair.
                searchFrom = close + 2;
                continue;
            }

            if (valid && c == '_' && close + length < text.Length && char.IsLetterOrDigit(text[close + length]))
            {
                valid = false;
            }

            if (!valid)
            {
                searchFrom = close + 1;
                continue;
            }

            if (doubled)
            {
                // "***" closing a "**": the strong delimiter is the end of the run, so that "**a *b***" nests correctly.
                var runEnd = close;
                while (runEnd < text.Length && text[runEnd] == c)
                {
                    runEnd++;
                }

                close = Math.Max(close, runEnd - length);
            }

            var tag = c == '~' ? "del" : doubled ? "strong" : "em";
            sb.Append('<').Append(tag).Append('>');
            Render(text[contentStart..close], sb, depth + 1, allowLinks);
            sb.Append("</").Append(tag).Append('>');
            end = close + length;
            return true;
        }

        return false;
    }

    private static bool TryRenderLink(string text, int start, StringBuilder sb, int depth, bool isImage, out int end)
    {
        end = start;
        if (!TryParseLink(text, start, out var label, out var url, out var title, out var linkEnd))
        {
            return false;
        }

        end = linkEnd;
        var safe = MarkdownHtml.IsSafeUrl(url) && (!isImage || MarkdownHtml.IsExternal(url) || url.StartsWith('/'));
        if (!safe)
        {
            // Unsafe target: keep the label as plain text, drop the link.
            Render(label, sb, depth + 1, allowLinks: false);
            return true;
        }

        sb.Append("<a href=\"");
        MarkdownHtml.Encode(url, sb);
        sb.Append('"');
        if (!string.IsNullOrEmpty(title))
        {
            sb.Append(" title=\"");
            MarkdownHtml.Encode(title, sb);
            sb.Append('"');
        }

        if (MarkdownHtml.IsExternal(url))
        {
            sb.Append(" target=\"_blank\" rel=\"noopener noreferrer nofollow\"");
        }

        if (isImage)
        {
            // Images are not embedded (tracking pixels, mixed content); they are rendered as links instead.
            sb.Append(" class=\"md-image-link\">");
            MarkdownHtml.Encode(label.Length == 0 ? "Image" : label, sb);
        }
        else
        {
            sb.Append('>');
            Render(label, sb, depth + 1, allowLinks: false);
        }

        sb.Append("</a>");
        return true;
    }

    private static bool TryParseLink(string text, int start, out string label, out string url, out string? title, out int end)
    {
        label = url = string.Empty;
        title = null;
        end = start;

        // Label: matching closing bracket (nested brackets and escapes allowed).
        var nesting = 0;
        var i = start;
        var labelEnd = -1;
        for (; i < text.Length && i - start <= MaxLabelLength; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                i++;
                continue;
            }

            if (c == '[')
            {
                nesting++;
            }
            else if (c == ']')
            {
                nesting--;
                if (nesting == 0)
                {
                    labelEnd = i;
                    break;
                }
            }
        }

        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        label = text[(start + 1)..labelEnd];
        i = labelEnd + 2;
        while (i < text.Length && text[i] == ' ')
        {
            i++;
        }

        // Destination.
        var destination = new StringBuilder();
        if (i < text.Length && text[i] == '<')
        {
            i++;
            while (i < text.Length && text[i] != '>' && text[i] != '\n')
            {
                destination.Append(text[i++]);
            }

            if (i >= text.Length || text[i] != '>')
            {
                return false;
            }

            i++;
        }
        else
        {
            var parens = 0;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                var c = text[i];
                if (c == '(')
                {
                    parens++;
                }
                else if (c == ')')
                {
                    if (parens == 0)
                    {
                        break;
                    }

                    parens--;
                }

                destination.Append(c);
                i++;
            }
        }

        while (i < text.Length && text[i] == ' ')
        {
            i++;
        }

        // Optional title.
        if (i < text.Length && (text[i] == '"' || text[i] == '\''))
        {
            var quote = text[i];
            var close = text.IndexOf(quote, i + 1);
            if (close < 0)
            {
                return false;
            }

            title = text[(i + 1)..close];
            i = close + 1;
            while (i < text.Length && text[i] == ' ')
            {
                i++;
            }
        }

        if (i >= text.Length || text[i] != ')')
        {
            return false;
        }

        url = destination.ToString();
        end = i + 1;
        return true;
    }

    private static bool TryRenderAutolink(string text, int start, StringBuilder sb, out int end)
    {
        end = start;
        var close = text.IndexOf('>', start + 1);
        if (close < 0 || close - start > 2048)
        {
            return false;
        }

        var url = text[(start + 1)..close];
        if (!(MarkdownHtml.IsExternal(url) || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) || !MarkdownHtml.IsSafeUrl(url))
        {
            return false;
        }

        AppendLink(url, url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? url[7..] : url, sb);
        end = close + 1;
        return true;
    }

    private static bool TryRenderBareUrl(string text, int start, StringBuilder sb, out int end)
    {
        end = start;
        if (!MarkdownHtml.IsExternal(text[start..Math.Min(text.Length, start + 8)]))
        {
            return false;
        }

        var i = start;
        while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('<' or '>' or '"' or '\'' or '`'))
        {
            i++;
        }

        while (i > start && ".,;:!?)]*_~".Contains(text[i - 1]))
        {
            i--;
        }

        var url = text[start..i];
        if (!MarkdownHtml.IsSafeUrl(url))
        {
            return false;
        }

        AppendLink(url, url, sb);
        end = i;
        return true;
    }

    private static void AppendLink(string url, string display, StringBuilder sb)
    {
        sb.Append("<a href=\"");
        MarkdownHtml.Encode(url, sb);
        sb.Append('"');
        if (MarkdownHtml.IsExternal(url))
        {
            sb.Append(" target=\"_blank\" rel=\"noopener noreferrer nofollow\"");
        }

        sb.Append('>');
        MarkdownHtml.Encode(display, sb);
        sb.Append("</a>");
    }
}
