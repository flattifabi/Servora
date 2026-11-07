using Servora.Domain.Common;

namespace Servora.Domain.Portal;

/// <summary>
/// An announcement shown to every portal user on the portal home page while it is active and inside its display window,
/// for example planned maintenance or a known outage.
/// </summary>
[RecordTable(PortalTables.Announcement, "Portal Announcement", PluralLabel = "Portal Announcements", DisplayField = nameof(Title), Icon = "bell", Route = "/portal/admin/announcements")]
public class PortalAnnouncement : AuditableEntity
{
    /// <summary>The maximum length of the title.</summary>
    public const int TitleMaxLength = 160;

    /// <summary>The maximum length of the body.</summary>
    public const int BodyMaxLength = 2000;

    /// <summary>The maximum length of the link.</summary>
    public const int LinkMaxLength = 500;

    /// <summary>The maximum length of the link text.</summary>
    public const int LinkTextMaxLength = 80;

    /// <summary>Gets or sets the headline.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain-text message.</summary>
    public string? Body { get; set; }

    /// <summary>Gets or sets the visual kind.</summary>
    public PortalAnnouncementKind Kind { get; set; } = PortalAnnouncementKind.Info;

    /// <summary>Gets or sets an optional link (site-relative path or absolute HTTPS URL).</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Gets or sets the text of the link.</summary>
    public string? LinkText { get; set; }

    /// <summary>Gets or sets the UTC time from which the announcement is shown.</summary>
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Gets or sets the UTC time after which the announcement is hidden (<see langword="null"/> = until deactivated).</summary>
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the announcement is published.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the display order (lower first; outages are always shown first).</summary>
    public int Order { get; set; }
}
