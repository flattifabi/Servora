using Servora.Domain.Common;

namespace Servora.Domain.Sla;

/// <summary>
/// An SLA timer attached to one record. Elapsed schedule time is accumulated at every pause and stop; while running,
/// the current segment since <see cref="ActiveSince"/> is added on the fly.
/// </summary>
public class TaskSla : AuditableEntity
{
    /// <summary>Gets or sets the SLA definition.</summary>
    public Guid SlaDefinitionId { get; set; }

    /// <summary>Gets or sets the technical table name of the record.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the record identifier.</summary>
    public Guid RecordId { get; set; }

    /// <summary>Gets or sets the display value of the record at start time (for lists and messages).</summary>
    public string? RecordDisplay { get; set; }

    /// <summary>Gets or sets the stage.</summary>
    public SlaStage Stage { get; set; } = SlaStage.InProgress;

    /// <summary>Gets or sets the UTC start time.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets the UTC start of the current running segment (null while paused or final).</summary>
    public DateTimeOffset? ActiveSince { get; set; }

    /// <summary>Gets or sets the schedule seconds accumulated before <see cref="ActiveSince"/>.</summary>
    public long ElapsedSeconds { get; set; }

    /// <summary>Gets or sets the UTC planned breach time (null while paused).</summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>Gets or sets the UTC time the current pause began.</summary>
    public DateTimeOffset? PausedAt { get; set; }

    /// <summary>Gets or sets the UTC time the timer was completed or cancelled.</summary>
    public DateTimeOffset? StoppedAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the target was missed.</summary>
    public bool HasBreached { get; set; }

    /// <summary>Gets or sets the UTC time the breach was detected.</summary>
    public DateTimeOffset? BreachedAt { get; set; }
}
