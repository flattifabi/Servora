namespace Servora.Application.Knowledge.Markdown;

/// <summary>Result of rendering Markdown with <see cref="SafeMarkdown"/>.</summary>
/// <param name="Html">The sanitized HTML. Safe to output unencoded (for example as a Blazor <c>MarkupString</c>).</param>
/// <param name="Headings">The headings in document order.</param>
public sealed record MarkdownRenderResult(string Html, IReadOnlyList<MarkdownHeading> Headings);
