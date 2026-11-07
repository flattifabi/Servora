using Servora.Application.Knowledge.Markdown;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Feature tests for the supported Markdown subset of <see cref="SafeMarkdown"/>.</summary>
public sealed class SafeMarkdownRenderingTests
{
    [Fact]
    public void Headings_GetIdsAndAreCollected()
    {
        var result = SafeMarkdown.Render("# Title\n\n## Before you start\n### Step **one**");

        Assert.Contains("<h1 id=\"kb-title\" tabindex=\"-1\">Title</h1>", result.Html, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"kb-step-one\" tabindex=\"-1\">Step <strong>one</strong></h3>", result.Html, StringComparison.Ordinal);
        Assert.Equal([1, 2, 3], result.Headings.Select(h => h.Level));
        Assert.Equal("Step one", result.Headings[2].Text);
    }

    [Theory]
    [InlineData("**bold**", "<strong>bold</strong>")]
    [InlineData("__bold__", "<strong>bold</strong>")]
    [InlineData("*italic*", "<em>italic</em>")]
    [InlineData("_italic_", "<em>italic</em>")]
    [InlineData("~~gone~~", "<del>gone</del>")]
    [InlineData("`a < b`", "<code>a &lt; b</code>")]
    [InlineData("**bold *and italic***", "<strong>bold <em>and italic</em></strong>")]
    [InlineData("snake_case_name", "snake_case_name")]
    [InlineData("2 * 3 * 4", "2 * 3 * 4")]
    [InlineData("\\*not italic\\*", "*not italic*")]
    public void Inline_Formatting(string markdown, string expected)
    {
        Assert.Equal($"<p>{expected}</p>\n", SafeMarkdown.ToHtml(markdown));
    }

    [Fact]
    public void Links_Relative_Mailto_AndAutolinks()
    {
        var html = SafeMarkdown.ToHtml("See [the guide](/knowledge/1 \"Guide\"), mail <mailto:help@example.com> or visit https://example.com/a.");

        Assert.Contains("<a href=\"/knowledge/1\" title=\"Guide\">the guide</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"mailto:help@example.com\">help@example.com</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://example.com/a\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">https://example.com/a</a>.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void FencedCode_IsEncodedAndKeepsLanguage()
    {
        var html = SafeMarkdown.ToHtml("```sql\nSELECT * FROM t WHERE a < 3;\n```");

        Assert.Equal("<pre><code class=\"language-sql\">SELECT * FROM t WHERE a &lt; 3;\n</code></pre>\n", html);
    }

    [Fact]
    public void Lists_OrderedUnorderedAndNested()
    {
        var html = SafeMarkdown.ToHtml("1. First\n2. Second\n   - nested a\n   - nested b\n3. Third\n\n- x\n- y");

        Assert.Contains("<ol>\n<li>First</li>\n<li>Second\n<ul>\n<li>nested a</li>\n<li>nested b</li>\n</ul>\n</li>\n<li>Third</li>\n</ol>", html, StringComparison.Ordinal);
        Assert.Contains("<ul>\n<li>x</li>\n<li>y</li>\n</ul>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderedList_KeepsStartNumber()
    {
        Assert.StartsWith("<ol start=\"3\">", SafeMarkdown.ToHtml("3. three\n4. four"), StringComparison.Ordinal);
    }

    [Fact]
    public void Blockquote_And_HorizontalRule()
    {
        var html = SafeMarkdown.ToHtml("> **Tip:** quoted\n> second line\n\n---\n\nafter");

        Assert.Contains("<blockquote>\n<p><strong>Tip:</strong> quoted\nsecond line</p>\n</blockquote>", html, StringComparison.Ordinal);
        Assert.Contains("<hr />", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_WithAlignment()
    {
        var html = SafeMarkdown.ToHtml("| Name | Count |\n|:---|---:|\n| a | **1** |\n| b |");

        Assert.Contains("<th class=\"align-left\">Name</th><th class=\"align-right\">Count</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"align-left\">a</td><td class=\"align-right\"><strong>1</strong></td>", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"align-left\">b</td><td class=\"align-right\"></td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HardLineBreak_WithTwoSpaces()
    {
        Assert.Equal("<p>line one<br />\nline two</p>\n", SafeMarkdown.ToHtml("line one  \nline two"));
    }

    [Fact]
    public void ToPlainText_StripsMarkup()
    {
        Assert.Equal("Title Some bold text & code", SafeMarkdown.ToPlainText("# Title\n\nSome **bold** text & `code`"));
    }

    [Fact]
    public void EmptyInput_RendersNothing()
    {
        Assert.Equal(string.Empty, SafeMarkdown.ToHtml(null));
        Assert.Empty(SafeMarkdown.Render("   \n  ").Headings);
    }
}
