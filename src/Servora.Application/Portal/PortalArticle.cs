using Servora.Application.Knowledge.Markdown;

namespace Servora.Application.Portal;

/// <summary>A published knowledge article rendered for the portal reader.</summary>
public sealed record PortalArticle
{
    /// <summary>Gets the list data of the article.</summary>
    public PortalArticleSummary Summary { get; init; } = new();

    /// <summary>Gets the body rendered by the XSS-safe <see cref="SafeMarkdown"/> renderer.</summary>
    public string SafeHtml { get; init; } = string.Empty;

    /// <summary>Gets the headings of the body (table of contents).</summary>
    public IReadOnlyList<MarkdownHeading> Headings { get; init; } = [];

    /// <summary>Gets the author display name.</summary>
    public string? AuthorName { get; init; }

    /// <summary>Gets the UTC time of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the number of helpful votes.</summary>
    public int HelpfulCount { get; init; }

    /// <summary>Gets the number of not-helpful votes.</summary>
    public int NotHelpfulCount { get; init; }

    /// <summary>Gets the current user's vote (<see langword="null"/> when the user has not voted).</summary>
    public bool? MyVote { get; init; }

    /// <summary>Gets other published articles of the same category.</summary>
    public IReadOnlyList<PortalArticleSummary> Related { get; init; } = [];
}
