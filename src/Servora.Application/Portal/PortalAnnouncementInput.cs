using Servora.Domain.Portal;

namespace Servora.Application.Portal;

/// <summary>Editable fields of a portal announcement.</summary>
public sealed class PortalAnnouncementInput
{
    /// <summary>Gets or sets the headline.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain-text message.</summary>
    public string? Body { get; set; }

    /// <summary>Gets or sets the kind.</summary>
    public PortalAnnouncementKind Kind { get; set; } = PortalAnnouncementKind.Info;

    /// <summary>Gets or sets the optional link (site-relative path such as <c>/portal/knowledge</c> or an absolute HTTPS URL).</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Gets or sets the link text.</summary>
    public string? LinkText { get; set; }

    /// <summary>Gets or sets the UTC start of the display window.</summary>
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Gets or sets the UTC end of the display window (<see langword="null"/> = open-ended).</summary>
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the announcement is published.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the display order.</summary>
    public int Order { get; set; }

    /// <summary>Gets or sets the concurrency version of the edited announcement (ignored for new announcements).</summary>
    public long Version { get; set; }

    /// <summary>Validates the input.</summary>
    /// <returns>A user-safe error message, or <see langword="null"/> when the input is valid.</returns>
    public string? Validate()
    {
        var title = Title?.Trim() ?? string.Empty;
        if (title.Length == 0)
        {
            return "Enter a title.";
        }

        if (title.Length > PortalAnnouncement.TitleMaxLength)
        {
            return $"The title must not exceed {PortalAnnouncement.TitleMaxLength} characters.";
        }

        if (Body is { Length: > PortalAnnouncement.BodyMaxLength })
        {
            return $"The message must not exceed {PortalAnnouncement.BodyMaxLength} characters.";
        }

        if (!Enum.IsDefined(Kind))
        {
            return "Select a valid kind.";
        }

        if (EndsAt is { } end && end <= StartsAt)
        {
            return "The end of the display window must be after its start.";
        }

        if (!string.IsNullOrWhiteSpace(LinkUrl))
        {
            var link = LinkUrl.Trim();
            if (link.Length > PortalAnnouncement.LinkMaxLength || !PortalLinkPolicy.IsAllowed(link))
            {
                return "The link must be a site-relative path (for example /portal/knowledge) or an absolute https:// URL.";
            }
        }

        if (LinkText is { Length: > PortalAnnouncement.LinkTextMaxLength })
        {
            return $"The link text must not exceed {PortalAnnouncement.LinkTextMaxLength} characters.";
        }

        return null;
    }
}
