namespace Servora.Web.Components.KnowledgeCmdb;

/// <summary>A text segment produced by <see cref="SearchHighlighter"/>.</summary>
/// <param name="Text">The text.</param>
/// <param name="IsMatch">Whether the segment matches a search term.</param>
public sealed record HighlightSegment(string Text, bool IsMatch);
