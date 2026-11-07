namespace Servora.Application.Configuration;

/// <summary>Key figures of the mail outbox.</summary>
/// <param name="Pending">Messages waiting for delivery (including retries).</param>
/// <param name="Retrying">Pending messages that already failed at least once.</param>
/// <param name="SentLast24Hours">Messages delivered in the last 24 hours.</param>
/// <param name="Failed">Messages that failed permanently.</param>
public sealed record OutboxStatistics(int Pending, int Retrying, int SentLast24Hours, int Failed);
