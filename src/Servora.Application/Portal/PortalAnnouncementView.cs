using Servora.Domain.Portal;

namespace Servora.Application.Portal;

/// <summary>An announcement as shown on the portal (and in the announcement administration).</summary>
public sealed record PortalAnnouncementView
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the headline.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the plain-text message.</summary>
    public string? Body { get; init; }

    /// <summary>Gets the kind.</summary>
    public PortalAnnouncementKind Kind { get; init; }

    /// <summary>Gets the optional, validated link.</summary>
    public string? LinkUrl { get; init; }

    /// <summary>Gets the link text.</summary>
    public string? LinkText { get; init; }

    /// <summary>Gets the UTC start of the display window.</summary>
    public DateTimeOffset StartsAt { get; init; }

    /// <summary>Gets the UTC end of the display window.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Gets a value indicating whether the announcement is published.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets the display order.</summary>
    public int Order { get; init; }

    /// <summary>Gets a value indicating whether the announcement is currently visible on the portal.</summary>
    public bool IsLive { get; init; }

    /// <summary>Gets the concurrency version (administration only).</summary>
    public long Version { get; init; }

    /// <summary>Gets the UTC time of the last change.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the name of the last editor.</summary>
    public string? UpdatedBy { get; init; }
}
