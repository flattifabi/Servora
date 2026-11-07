using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Servora.Application.Knowledge.Markdown;

/// <summary>HTML encoding, URL validation and slug helpers of the safe Markdown renderer.</summary>
internal static partial class MarkdownHtml
{
    private const int MaxUrlLength = 2048;

    /// <summary>HTML-encodes a string (text and attribute safe) and appends it.</summary>
    /// <param name="text">The raw text.</param>
    /// <param name="sb">The output.</param>
    public static void Encode(string text, StringBuilder sb)
    {
        foreach (var c in text)
        {
            Encode(c, sb);
        }
    }

    /// <summary>HTML-encodes a character (text and attribute safe) and appends it.</summary>
    /// <param name="c">The character.</param>
    /// <param name="sb">The output.</param>
    public static void Encode(char c, StringBuilder sb)
    {
        switch (c)
        {
            case '&': sb.Append("&amp;"); break;
            case '<': sb.Append("&lt;"); break;
            case '>': sb.Append("&gt;"); break;
            case '"': sb.Append("&quot;"); break;
            case '\'': sb.Append("&#39;"); break;
            case '\0': sb.Append('�'); break;
            default: sb.Append(c); break;
        }
    }

    /// <summary>HTML-encodes a string.</summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The encoded text.</returns>
    public static string Encode(string text)
    {
        var sb = new StringBuilder(text.Length + 16);
        Encode(text, sb);
        return sb.ToString();
    }

    /// <summary>
    /// Checks whether a link target is allowed: absolute <c>http</c>/<c>https</c> URLs with a host, <c>mailto:</c> addresses,
    /// site-relative paths (<c>/path</c>, never protocol-relative <c>//host</c>) and fragments (<c>#id</c>). Everything else
    /// (<c>javascript:</c>, <c>data:</c>, <c>vbscript:</c>, obfuscated schemes, control characters, whitespace, backslashes) is rejected.
    /// </summary>
    /// <param name="url">The raw URL.</param>
    /// <returns><see langword="true"/> when the URL may be emitted as <c>href</c>.</returns>
    public static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url) || url.Length > MaxUrlLength)
        {
            return false;
        }

        foreach (var c in url)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\' || c == '<' || c == '>' || c == '"' || c == '\'' || c == '`')
            {
                return false;
            }
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrEmpty(uri.Host);
        }

        if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return url.Length > 7 && url.IndexOf(':', 7) < 0;
        }

        if (url[0] == '/')
        {
            return url.Length == 1 || url[1] != '/';
        }

        return url[0] == '#' && url.Length > 1;
    }

    /// <summary>Gets a value indicating whether a (safe) URL points to another site.</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true"/> for absolute http(s) URLs.</returns>
    public static bool IsExternal(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>Creates a DOM-safe element id from heading text. Ids are prefixed with <c>kb-</c> to prevent DOM clobbering.</summary>
    /// <param name="text">The plain heading text.</param>
    /// <returns>The slug.</returns>
    public static string Slugify(string text)
    {
        var sb = new StringBuilder("kb-");
        var lastDash = true;
        foreach (var c in text.ToLowerInvariant())
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                sb.Append(c);
                lastDash = false;
            }
            else if (!lastDash && sb.Length < 80)
            {
                sb.Append('-');
                lastDash = true;
            }

            if (sb.Length >= 80)
            {
                break;
            }
        }

        var slug = sb.ToString().TrimEnd('-');
        return slug.Length <= 3 ? "kb-section" : slug;
    }

    /// <summary>Converts rendered (safe) HTML back to plain text.</summary>
    /// <param name="html">The HTML produced by the renderer.</param>
    /// <returns>The plain text.</returns>
    public static string StripTags(string html)
    {
        var text = TagRegex().Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
