namespace Servora.Application.Notifications;

/// <summary>An e-mail template as edited in the UI.</summary>
public sealed class NotificationTemplateModel
{
    /// <summary>Gets or sets the identifier (empty for new templates).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the unique name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the subject template.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the body template.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC time of the last change (read only).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
