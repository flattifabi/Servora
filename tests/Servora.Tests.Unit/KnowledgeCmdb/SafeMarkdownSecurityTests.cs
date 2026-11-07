using System.Text.RegularExpressions;
using Servora.Application.Knowledge.Markdown;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>XSS safety tests for <see cref="SafeMarkdown"/>.</summary>
public sealed partial class SafeMarkdownSecurityTests
{
    private static readonly string[] s_allowedTags = ["p", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "em", "del", "code", "pre", "blockquote", "ul", "ol", "li", "a", "hr", "br", "table", "thead", "tbody", "tr", "th", "td", "div"];
    private static readonly string[] s_allowedAttributes = ["href", "title", "target", "rel", "id", "tabindex", "class", "start"];

    public static TheoryData<string> Payloads => new()
    {
        "<script>alert(1)</script>",
        "<img src=x onerror=alert(1)>",
        "<svg/onload=alert(1)>",
        "<iframe src=\"javascript:alert(1)\"></iframe>",
        "[click](javascript:alert(1))",
        "[click](JaVaScRiPt:alert(1))",
        "[click](  javascript:alert(1))",
        "[click](java\tscript:alert(1))",
        "[click](&#106;avascript:alert(1))",
        "[click](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)",
        "[click](vbscript:msgbox(1))",
        "[click](//evil.example.com)",
        "[click](/\\evil.example.com)",
        "[x](https://example.com\" onmouseover=\"alert(1))",
        "[x](https://example.com 'a\" onmouseover=\"alert(1)')",
        "![img](javascript:alert(1))",
        "![a\" onerror=\"alert(1)](https://example.com/a.png)",
        "<javascript:alert(1)>",
        "<a href=\"javascript:alert(1)\">x</a>",
        "**<b onclick=alert(1)>bold</b>**",
        "`<script>alert(1)</script>`",
        "```html\n<script>alert(1)</script>\n```",
        "```\"><script>alert(1)</script>\n<b>x</b>\n```",
        "# <img src=x onerror=alert(1)>",
        "> <script>alert(1)</script>",
        "- <script>alert(1)</script>",
        "| <script>x</script> | b |\n|---|---|\n| <img src=x onerror=alert(1)> | d |",
        "<style>body{display:none}</style>",
        "\"><script>alert(1)</script>",
        "<<script>script>alert(1)<</script>/script>",
        "[a](https://example.com/\"><script>alert(1)</script>)",
        "https://example.com/\"onmouseover=\"alert(1)",
        "\0<script>alert(1)</script>",
    };

    [Theory]
    [MemberData(nameof(Payloads))]
    public void Payload_ProducesOnlyAllowedTagsAttributesAndSchemes(string markdown)
    {
        var html = SafeMarkdown.ToHtml(markdown);

        AssertSafe(html);
    }

    [Fact]
    public void RawHtml_IsEncoded()
    {
        var html = SafeMarkdown.ToHtml("Hello <b>world</b> & <script>alert('x')</script>");

        Assert.Equal("<p>Hello &lt;b&gt;world&lt;/b&gt; &amp; &lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;</p>\n", html);
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))", "click")]
    [InlineData("[click](data:text/html,hi)", "click")]
    [InlineData("[click](//evil.example.com)", "click")]
    public void UnsafeLink_KeepsLabelButDropsAnchor(string markdown, string label)
    {
        var html = SafeMarkdown.ToHtml(markdown);

        Assert.DoesNotContain("<a", html, StringComparison.Ordinal);
        Assert.Contains(label, html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com/path?q=1", true)]
    [InlineData("http://example.com", true)]
    [InlineData("mailto:help@example.com", true)]
    [InlineData("/knowledge/123", true)]
    [InlineData("#section", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JAVASCRIPT:alert(1)", false)]
    [InlineData("data:text/html,x", false)]
    [InlineData("vbscript:x", false)]
    [InlineData("//evil.com", false)]
    [InlineData("/\\evil.com", false)]
    [InlineData("https://", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("relative/path", false)]
    [InlineData("https://exa mple.com", false)]
    [InlineData("https://example.com/\u0001", false)]
    [InlineData("", false)]
    public void IsSafeUrl_AllowsOnlyWhitelistedTargets(string url, bool expected) => Assert.Equal(expected, SafeMarkdown.IsSafeUrl(url));

    [Fact]
    public void ExternalLinks_OpenInNewTabWithoutOpener()
    {
        var html = SafeMarkdown.ToHtml("[Docs](https://example.com/docs)");

        Assert.Contains("<a href=\"https://example.com/docs\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">Docs</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Images_AreRenderedAsLinksNotEmbedded()
    {
        var html = SafeMarkdown.ToHtml("![Diagram](https://example.com/diagram.png)");

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("class=\"md-image-link\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HeadingIds_ArePrefixedAgainstDomClobbering()
    {
        var result = SafeMarkdown.Render("# servora\n## location\n## location");

        Assert.All(result.Headings, h => Assert.StartsWith("kb-", h.Id, StringComparison.Ordinal));
        Assert.Equal(["kb-servora", "kb-location", "kb-location-2"], result.Headings.Select(h => h.Id));
    }

    [Fact]
    public void DeeplyNestedInput_DoesNotOverflowAndStaysSafe()
    {
        var markdown = string.Concat(Enumerable.Repeat("> ", 500)) + "<script>x</script>";
        markdown += "\n\n" + string.Concat(Enumerable.Repeat("**", 3000)) + "<img>";
        markdown += "\n\n" + string.Concat(Enumerable.Repeat("[", 5000));

        var html = SafeMarkdown.ToHtml(markdown);

        AssertSafe(html);
    }

    [Fact]
    public void PathologicalEmphasis_RendersQuickly()
    {
        var markdown = string.Concat(Enumerable.Repeat("**a ", 20_000));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        SafeMarkdown.ToHtml(markdown);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Rendering took {watch.Elapsed}.");
    }

    private static void AssertSafe(string html)
    {
        foreach (Match tag in TagRegex().Matches(html))
        {
            var name = tag.Groups["name"].Value.ToLowerInvariant();
            Assert.Contains(name, s_allowedTags);
            foreach (Match attribute in AttributeRegex().Matches(tag.Groups["attrs"].Value))
            {
                Assert.Contains(attribute.Groups["name"].Value.ToLowerInvariant(), s_allowedAttributes);
                if (attribute.Groups["name"].Value == "href")
                {
                    Assert.True(SafeMarkdown.IsSafeUrl(System.Net.WebUtility.HtmlDecode(attribute.Groups["value"].Value)), $"Unsafe href in: {html}");
                }
            }

            // Everything between the attributes must be well-formed name="value" pairs (no injected handlers).
            var leftover = AttributeRegex().Replace(tag.Groups["attrs"].Value, string.Empty).Trim().TrimEnd('/').Trim();
            Assert.True(leftover.Length == 0, $"Unexpected attribute content '{leftover}' in: {html}");
        }

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<svg", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\0', html);
    }

    [GeneratedRegex("<(?!/)(?<name>[a-zA-Z0-9]+)(?<attrs>[^>]*)>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex("(?<name>[a-zA-Z-]+)=\"(?<value>[^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeRegex();
}
