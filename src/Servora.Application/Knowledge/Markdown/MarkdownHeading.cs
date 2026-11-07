namespace Servora.Application.Knowledge.Markdown;

/// <summary>A heading found while rendering Markdown; used to build a table of contents.</summary>
/// <param name="Level">The heading level (1–6).</param>
/// <param name="Text">The plain heading text.</param>
/// <param name="Id">The element id of the rendered heading (always prefixed with <c>kb-</c>).</param>
public sealed record MarkdownHeading(int Level, string Text, string Id);
