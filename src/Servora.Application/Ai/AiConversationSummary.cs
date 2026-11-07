namespace Servora.Application.Ai;

/// <summary>Conversation entry of the history sidebar.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="LastMessageAt">The UTC time of the last message.</param>
/// <param name="MessageCount">The number of messages.</param>
public sealed record AiConversationSummary(Guid Id, string Title, DateTimeOffset LastMessageAt, int MessageCount);
