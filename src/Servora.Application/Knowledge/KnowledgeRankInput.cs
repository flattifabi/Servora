namespace Servora.Application.Knowledge;

/// <summary>The text and popularity signals of one article used by <see cref="KnowledgeRanker"/>.</summary>
/// <param name="Title">The title.</param>
/// <param name="Summary">The summary.</param>
/// <param name="Keywords">The comma separated keywords.</param>
/// <param name="PlainBody">The body converted to plain text.</param>
/// <param name="ViewCount">The number of views.</param>
/// <param name="HelpfulCount">The number of helpful votes.</param>
/// <param name="NotHelpfulCount">The number of not helpful votes.</param>
public sealed record KnowledgeRankInput(string Title, string? Summary, string? Keywords, string PlainBody, int ViewCount = 0, int HelpfulCount = 0, int NotHelpfulCount = 0);
