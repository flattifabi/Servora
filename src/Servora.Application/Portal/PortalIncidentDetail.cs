namespace Servora.Application.Portal;

/// <summary>An incident as shown to its caller in the portal (no internal fields such as work notes).</summary>
public sealed record PortalIncidentDetail
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the number.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Gets the title (short description).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the description entered by the caller.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the consumer-friendly status.</summary>
    public PortalStatus Status { get; init; } = new("Submitted", "info", true);

    /// <summary>Gets the status timeline.</summary>
    public IReadOnlyList<PortalProgressStep> Steps { get; init; } = [];

    /// <summary>Gets the key facts (category, urgency, priority, team, ...).</summary>
    public IReadOnlyList<PortalFact> Facts { get; init; } = [];

    /// <summary>Gets the caller display name.</summary>
    public string? CallerName { get; init; }

    /// <summary>Gets the display name of the user who opened the incident.</summary>
    public string? OpenedByName { get; init; }

    /// <summary>Gets the name of the team working on the incident.</summary>
    public string? AssignmentGroupName { get; init; }

    /// <summary>Gets the name of the person working on the incident.</summary>
    public string? AssignedToName { get; init; }

    /// <summary>Gets the resolution summary shown when the incident is resolved or closed.</summary>
    public string? ResolutionNotes { get; init; }

    /// <summary>Gets the resolution code label.</summary>
    public string? ResolutionLabel { get; init; }

    /// <summary>Gets the UTC time the incident was opened.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets the UTC time of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the UTC time the incident was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>Gets the UTC time the incident was closed.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Gets a value indicating whether the caller can accept the resolution (close the incident).</summary>
    public bool CanAcceptResolution { get; init; }

    /// <summary>Gets a value indicating whether the caller can reopen the incident.</summary>
    public bool CanReopen { get; init; }

    /// <summary>Gets a value indicating whether comments and attachments can be added.</summary>
    public bool CanComment { get; init; }
}
