using Servora.Domain.Configuration;

namespace Servora.Application.Configuration;

/// <summary>A row of the mail outbox list.</summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Recipients">The recipients (semicolon separated).</param>
/// <param name="Subject">The subject.</param>
/// <param name="Status">The delivery state.</param>
/// <param name="Attempts">The number of attempts made.</param>
/// <param name="CreatedAt">When the message was queued.</param>
/// <param name="NextAttemptAt">When the next attempt is due.</param>
/// <param name="SentAt">When the message was accepted by the server.</param>
/// <param name="LastError">The last delivery error.</param>
/// <param name="Source">The producer label.</param>
public sealed record OutboxMessageSummary(
    Guid Id,
    string Recipients,
    string Subject,
    OutboundEmailStatus Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? SentAt,
    string? LastError,
    string? Source);
