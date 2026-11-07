using Microsoft.Extensions.Logging;

namespace Servora.Application.Observability;

/// <summary>
/// One captured log event. Message, exception text and property values are already redacted (passwords, tokens,
/// connection string secrets) when the entry is created by the logger provider.
/// </summary>
/// <param name="Sequence">The process-wide, monotonically increasing sequence number.</param>
/// <param name="Timestamp">The UTC timestamp.</param>
/// <param name="Level">The log level.</param>
/// <param name="Category">The logger category (usually the full type name).</param>
/// <param name="EventId">The numeric event id.</param>
/// <param name="EventName">The event name, if any.</param>
/// <param name="Message">The rendered, redacted message.</param>
/// <param name="Exception">The redacted exception text (type, message, stack trace), if any.</param>
/// <param name="CorrelationId">The correlation id of the request / execution, if known.</param>
/// <param name="Properties">Structured state and scope values (redacted, bounded).</param>
public sealed record LogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    int EventId,
    string? EventName,
    string Message,
    string? Exception,
    string? CorrelationId,
    IReadOnlyDictionary<string, string?> Properties);
