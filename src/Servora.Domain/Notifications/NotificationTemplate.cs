using Servora.Domain.Common;

namespace Servora.Domain.Notifications;

/// <summary>
/// Reusable e-mail template. <see cref="Subject"/> and <see cref="Body"/> are plain text with <c>${Field}</c>
/// placeholders; values are HTML-encoded when the message is rendered.
/// </summary>
public class NotificationTemplate : AuditableEntity
{
    /// <summary>Gets or sets the unique template name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the subject template.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the body template (plain text, line breaks are preserved).</summary>
    public string Body { get; set; } = string.Empty;
}
