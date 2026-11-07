namespace Servora.Domain.Itsm;

/// <summary>Lifecycle states of an <see cref="Incident"/>. Values follow the common ITSM numbering.</summary>
public enum IncidentState
{
    /// <summary>Logged but not yet worked on.</summary>
    New = 1,

    /// <summary>A fulfiller is actively working on the incident.</summary>
    InProgress = 2,

    /// <summary>Work is paused, for example while waiting for the caller or a vendor (requires an <see cref="OnHoldReason"/>).</summary>
    OnHold = 3,

    /// <summary>Service was restored; waiting for confirmation (requires a resolution code and notes).</summary>
    Resolved = 6,

    /// <summary>Final state: the resolution was accepted.</summary>
    Closed = 7,

    /// <summary>Final state: the incident was logged in error or is a duplicate.</summary>
    Cancelled = 8,
}
