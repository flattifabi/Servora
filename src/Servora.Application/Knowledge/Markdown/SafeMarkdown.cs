namespace Servora.Application.Knowledge.Markdown;

/// <summary>
/// In-house, XSS-safe Markdown renderer for knowledge articles (no external packages).
/// </summary>
/// <remarks>
/// <para>Security model: the renderer never passes raw HTML through. Every character of the source is HTML-encoded unless it is
/// part of recognised Markdown syntax, and the renderer only emits a fixed set of tags (<c>p h1-h6 strong em del code pre
/// blockquote ul ol li a hr br table thead tbody tr th td div</c>) with a fixed set of attributes (<c>href title target rel id
/// tabindex class start</c>). Link targets are restricted to <c>http</c>, <c>https</c>, <c>mailto</c>, site-relative paths and
/// fragments (see <see cref="IsSafeUrl"/>); images are rendered as links, never embedded. Heading ids are prefixed with
/// <c>kb-</c> to prevent DOM clobbering. Input is capped at <see cref="MaxInputLength"/> characters and nesting is bounded.</para>
/// <para>Supported subset: ATX headings, paragraphs and hard line breaks, <c>**strong**</c>, <c>*emphasis*</c>,
/// <c>~~strikethrough~~</c>, inline code, fenced code blocks, block quotes, nested ordered/unordered lists, links, autolinks,
/// horizontal rules and GitHub-style tables.</para>
/// </remarks>
public static class SafeMarkdown
{
    /// <summary>The maximum number of source characters that are rendered; longer input is truncated.</summary>
    public const int MaxInputLength = 100_000;

    /// <summary>Renders Markdown to sanitized HTML and collects the headings.</summary>
    /// <param name="markdown">The Markdown source (may be <see langword="null"/>).</param>
    /// <returns>The rendered result.</returns>
    public static MarkdownRenderResult Render(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return new MarkdownRenderResult(string.Empty, []);
        }

        if (markdown.Length > MaxInputLength)
        {
            markdown = markdown[..MaxInputLength];
        }

        return new MarkdownBlockRenderer().Render(markdown);
    }

    /// <summary>Renders Markdown to sanitized HTML.</summary>
    /// <param name="markdown">The Markdown source.</param>
    /// <returns>The HTML.</returns>
    public static string ToHtml(string? markdown) => Render(markdown).Html;

    /// <summary>Converts Markdown to plain text (for search snippets and AI context).</summary>
    /// <param name="markdown">The Markdown source.</param>
    /// <returns>The plain text with collapsed whitespace.</returns>
    public static string ToPlainText(string? markdown) => MarkdownHtml.StripTags(ToHtml(markdown));

    /// <summary>Checks whether a URL is an allowed link target (http, https, mailto, site-relative path or fragment).</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public static bool IsSafeUrl(string? url) => MarkdownHtml.IsSafeUrl(url);
}
