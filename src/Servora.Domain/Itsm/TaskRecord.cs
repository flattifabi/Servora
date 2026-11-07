using Servora.Domain.Common;

namespace Servora.Domain.Itsm;

/// <summary>
/// Shared shape of all work items (incidents, problems, changes, requests, catalog tasks). This is a C#-only base
/// class: every concrete task type is mapped to its own database table (see ADR-005).
/// </summary>
public abstract class TaskRecord : AuditableEntity, IHasCustomFields
{
    /// <summary>Gets or sets the human readable unique number, for example <c>INC0001001</c>.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets the one-line summary.</summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the detailed description (plain text or sanitized markdown).</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the business impact.</summary>
    public Impact Impact { get; set; } = Impact.Low;

    /// <summary>Gets or sets the urgency.</summary>
    public Urgency Urgency { get; set; } = Urgency.Low;

    /// <summary>Gets or sets the priority. Normally calculated from impact and urgency.</summary>
    public Priority Priority { get; set; } = Priority.Planning;

    /// <summary>Gets or sets the group responsible for the work.</summary>
    [Reference(TableNames.Group)]
    public Guid? AssignmentGroupId { get; set; }

    /// <summary>Gets or sets the user working on the record.</summary>
    [Reference(TableNames.User)]
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the user that opened the record.</summary>
    [Reference(TableNames.User)]
    public Guid? OpenedById { get; set; }

    /// <summary>Gets or sets the UTC time the record was opened.</summary>
    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>Gets or sets the affected configuration item.</summary>
    [Reference(TableNames.ConfigurationItem)]
    public Guid? ConfigurationItemId { get; set; }

    /// <summary>Gets or sets the UTC due date.</summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>Gets or sets the UTC time the record was closed.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Gets or sets the user that closed the record.</summary>
    [Reference(TableNames.User)]
    public Guid? ClosedById { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the record is still active (not in a final state).
    /// Maintained by the owning service when the state changes.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>Gets or sets comma separated free-form tags / labels.</summary>
    public string? Tags { get; set; }

    /// <inheritdoc />
    public string? CustomFieldsJson { get; set; }
}
