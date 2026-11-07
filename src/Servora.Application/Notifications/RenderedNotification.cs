namespace Servora.Application.Notifications;

/// <summary>A rendered notification message.</summary>
/// <param name="Subject">The single-line subject.</param>
/// <param name="HtmlBody">The complete HTML e-mail document.</param>
/// <param name="TextBody">The plain text body.</param>
/// <param name="Link">The link to the record.</param>
public sealed record RenderedNotification(string Subject, string HtmlBody, string TextBody, string Link);
