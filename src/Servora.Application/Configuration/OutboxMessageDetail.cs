namespace Servora.Application.Configuration;

/// <summary>Full content of an outbox message for inspection. Bodies must be displayed encoded, never rendered as HTML.</summary>
/// <param name="Summary">The summary.</param>
/// <param name="CcRecipients">The carbon copy recipients.</param>
/// <param name="HtmlBody">The HTML body (display as text).</param>
/// <param name="TextBody">The plain text body.</param>
public sealed record OutboxMessageDetail(OutboxMessageSummary Summary, string? CcRecipients, string HtmlBody, string? TextBody);
