using Servora.Domain.Common;

namespace Servora.Domain.Knowledge;

/// <summary>A reader's helpfulness vote for an article (one vote per user and article).</summary>
public class KnowledgeFeedback : Entity
{
    /// <summary>Gets or sets the article.</summary>
    [Reference(TableNames.KnowledgeArticle)]
    public Guid ArticleId { get; set; }

    /// <summary>Gets or sets the voting user.</summary>
    [Reference(TableNames.User)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets a value indicating whether the article was helpful.</summary>
    public bool Helpful { get; set; }

    /// <summary>Gets or sets an optional comment.</summary>
    public string? Comment { get; set; }

    /// <summary>Gets or sets the UTC time of the vote.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
