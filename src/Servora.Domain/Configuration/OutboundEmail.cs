using Servora.Domain.Common;

namespace Servora.Domain.Configuration;

/// <summary>
/// An e-mail in the outbound mail queue (outbox). Notifications are queued first and delivered asynchronously by the
/// outbox processor with retries and exponential back-off, so a temporarily unavailable SMTP server never blocks or
/// loses business operations.
/// </summary>
public class OutboundEmail : AuditableEntity
{
    /// <summary>Gets or sets the recipient addresses, separated by semicolons.</summary>
    public string Recipients { get; set; } = string.Empty;

    /// <summary>Gets or sets the carbon copy addresses, separated by semicolons.</summary>
    public string? CcRecipients { get; set; }

    /// <summary>Gets or sets the subject (single line).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTML body (already encoded / sanitized by the producer).</summary>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional plain text alternative body.</summary>
    public string? TextBody { get; set; }

    /// <summary>Gets or sets the delivery state.</summary>
    public OutboundEmailStatus Status { get; set; } = OutboundEmailStatus.Pending;

    /// <summary>Gets or sets the number of delivery attempts made so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets the UTC time at which the next delivery attempt is due.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Gets or sets the UTC time of the last delivery attempt.</summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>Gets or sets the UTC time the SMTP server accepted the message.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Gets or sets the user-safe error of the last failed attempt (never contains credentials).</summary>
    public string? LastError { get; set; }

    /// <summary>Gets or sets a short label of the producer, for example <c>notification</c> or <c>test</c>.</summary>
    public string? Source { get; set; }
}
