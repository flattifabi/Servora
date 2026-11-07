namespace Servora.Application.Ai;

/// <summary>A conversation with its messages (oldest first).</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Messages">The messages.</param>
public sealed record AiConversationView(Guid Id, string Title, IReadOnlyList<AiMessageView> Messages);
