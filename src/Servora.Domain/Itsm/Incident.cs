using Servora.Domain.Common;

namespace Servora.Domain.Itsm;

/// <summary>
/// An unplanned interruption or reduction in quality of an IT service. The goal of incident management is to restore
/// normal service operation as quickly as possible. State changes are validated by <see cref="IncidentStateMachine"/>.
/// </summary>
[RecordTable(TableNames.Incident, "Incident", PluralLabel = "Incidents", Icon = "alert-triangle", Route = "/incidents")]
public class Incident : TaskRecord
{
    /// <summary>The number prefix of incidents.</summary>
    public const string NumberPrefix = "INC";

    /// <summary>Gets or sets the lifecycle state.</summary>
    public IncidentState State { get; set; } = IncidentState.New;

    /// <summary>Gets or sets the person who reported the incident or is affected by it.</summary>
    [Reference(TableNames.User)]
    public Guid? CallerId { get; set; }

    /// <summary>Gets or sets the category name (see <see cref="IncidentCategory"/>).</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the subcategory name.</summary>
    public string? Subcategory { get; set; }

    /// <summary>Gets or sets the channel through which the incident was reported.</summary>
    public ContactChannel Channel { get; set; } = ContactChannel.Portal;

    /// <summary>Gets or sets the affected business service (a configuration item of class business service).</summary>
    [Reference(TableNames.ConfigurationItem)]
    public Guid? BusinessServiceId { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a major incident with wide business impact.</summary>
    public bool IsMajorIncident { get; set; }

    /// <summary>Gets or sets the parent incident when this incident is a child of a (major) incident.</summary>
    [Reference(TableNames.Incident)]
    public Guid? ParentIncidentId { get; set; }

    /// <summary>Gets or sets the problem that investigates the underlying cause.</summary>
    [Reference(TableNames.Problem)]
    public Guid? ProblemId { get; set; }

    /// <summary>Gets or sets the reason for <see cref="IncidentState.OnHold"/>.</summary>
    public OnHoldReason? OnHoldReason { get; set; }

    /// <summary>Gets or sets the resolution code (mandatory when resolved).</summary>
    public IncidentResolutionCode? ResolutionCode { get; set; }

    /// <summary>Gets or sets the resolution notes (mandatory when resolved).</summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>Gets or sets how often the incident was reopened after resolution.</summary>
    public int ReopenCount { get; set; }

    /// <summary>Gets or sets how often the assignment group changed.</summary>
    public int ReassignmentCount { get; set; }

    /// <summary>Gets or sets the UTC time the incident was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Gets or sets the user that resolved the incident.</summary>
    [Reference(TableNames.User)]
    public Guid? ResolvedById { get; set; }
}
