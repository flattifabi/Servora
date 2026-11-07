namespace Servora.Application.Portal;

/// <summary>A public comment or a system event of a portal item (work notes are never included).</summary>
/// <param name="Id">The entry identifier.</param>
/// <param name="IsSystem">Whether the entry is a system event rather than a comment.</param>
/// <param name="AuthorName">The author display name.</param>
/// <param name="IsMine">Whether the current user wrote the comment.</param>
/// <param name="Text">The text.</param>
/// <param name="At">The UTC creation time.</param>
public sealed record PortalConversationEntry(Guid Id, bool IsSystem, string AuthorName, bool IsMine, string Text, DateTimeOffset At);
