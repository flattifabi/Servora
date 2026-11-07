namespace Servora.Domain.Portal;

/// <summary>Visual kind of a portal announcement.</summary>
public enum PortalAnnouncementKind
{
    /// <summary>General information.</summary>
    Info = 0,

    /// <summary>Planned maintenance window.</summary>
    Maintenance = 1,

    /// <summary>A service outage or major disruption.</summary>
    Outage = 2,

    /// <summary>Good news, for example a new service or a restored outage.</summary>
    Success = 3,
}
