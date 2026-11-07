using Servora.Domain.Common;

namespace Servora.Domain.Notifications;

/// <summary>A message in a user's in-app inbox (bell in the top bar).</summary>
public class InAppNotification : Entity
{
    /// <summary>Gets or sets the recipient user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the title (plain text).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the body (plain text).</summary>
    public string? Body { get; set; }

    /// <summary>Gets or sets the relative link to the related page.</summary>
    public string? Link { get; set; }

    /// <summary>Gets or sets the related table.</summary>
    public string? TableName { get; set; }

    /// <summary>Gets or sets the related record.</summary>
    public Guid? RecordId { get; set; }

    /// <summary>Gets or sets the UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC time the user read the notification.</summary>
    public DateTimeOffset? ReadAt { get; set; }
}
