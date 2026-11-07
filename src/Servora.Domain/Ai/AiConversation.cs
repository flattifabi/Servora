using Servora.Domain.Common;

namespace Servora.Domain.Ai;

/// <summary>A conversation of a user with the AI assistant.</summary>
public class AiConversation : AuditableEntity
{
    /// <summary>Gets or sets the owning user.</summary>
    [Reference(TableNames.User)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the title (derived from the first message).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC time of the last message.</summary>
    public DateTimeOffset LastMessageAt { get; set; }

    /// <summary>Gets or sets the number of messages.</summary>
    public int MessageCount { get; set; }
}
