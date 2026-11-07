namespace Servora.Application.Portal;

/// <summary>The public conversation (comments, system events, attachments) of a portal item.</summary>
/// <param name="Entries">The entries in chronological order (oldest first).</param>
/// <param name="Attachments">The attachments, newest first.</param>
/// <param name="CanPost">Whether the current user can add comments and files.</param>
public sealed record PortalConversation(IReadOnlyList<PortalConversationEntry> Entries, IReadOnlyList<PortalAttachment> Attachments, bool CanPost);
