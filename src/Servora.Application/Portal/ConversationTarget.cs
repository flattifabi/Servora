namespace Servora.Application.Portal;

/// <summary>A verified, user-owned record whose conversation is read or written.</summary>
/// <param name="Table">The technical table name.</param>
/// <param name="IsOpen">Whether the record still accepts comments and files.</param>
/// <param name="RequestItemIds">The requested items of a request (their public events are merged into the conversation).</param>
internal sealed record ConversationTarget(string Table, bool IsOpen, IReadOnlyList<Guid> RequestItemIds);
