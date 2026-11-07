using Servora.Domain.Common;

namespace Servora.Domain.Automation;

/// <summary>
/// An automation flow: one trigger plus an ordered tree of steps (stored as JSON). Designed in the Flow Designer and
/// executed by the flow engine as the flow's run-as identity (a named system actor).
/// </summary>
/// <remarks>Flows are configuration, not user records, so they are not a <see cref="RecordTableAttribute"/> table (no record events).</remarks>
public class Flow : AuditableEntity
{
    /// <summary>Gets or sets the unique display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets a value indicating whether the trigger is armed.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the trigger type.</summary>
    public FlowTriggerType TriggerType { get; set; } = FlowTriggerType.Manual;

    /// <summary>Gets or sets the technical table name for record triggers.</summary>
    public string? TriggerTable { get; set; }

    /// <summary>Gets or sets the serialized trigger condition (<c>ConditionGroup</c> JSON) for record triggers.</summary>
    public string? TriggerConditionJson { get; set; }

    /// <summary>Gets or sets the 5-field cron expression for schedule and data source poll triggers.</summary>
    public string? CronExpression { get; set; }

    /// <summary>Gets or sets the time zone identifier the cron expression is evaluated in (UTC when empty).</summary>
    public string? TimeZoneId { get; set; }

    /// <summary>Gets or sets the next UTC fire time of time-based triggers.</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>Gets or sets the UTC time the trigger last fired.</summary>
    public DateTimeOffset? LastTriggeredAt { get; set; }

    /// <summary>Gets or sets the polled data source for <see cref="FlowTriggerType.DataSourcePoll"/>.</summary>
    public Guid? PollDataSourceId { get; set; }

    /// <summary>Gets or sets the polled endpoint key.</summary>
    public string? PollEndpointKey { get; set; }

    /// <summary>Gets or sets the JSON object of poll parameter values.</summary>
    public string? PollParametersJson { get; set; }

    /// <summary>Gets or sets a value indicating whether the poll trigger only fires when the response changed.</summary>
    public bool PollOnlyOnChange { get; set; } = true;

    /// <summary>Gets or sets the SHA-256 hash of the last polled response.</summary>
    public string? LastPollHash { get; set; }

    /// <summary>Gets or sets the JSON array of input definitions (manual starts and sub-flows).</summary>
    public string? InputsJson { get; set; }

    /// <summary>Gets or sets the JSON array of step definitions (the step tree).</summary>
    public string StepsJson { get; set; } = "[]";

    /// <summary>Gets or sets the definition version, incremented whenever the definition is saved.</summary>
    public int DefinitionVersion { get; set; } = 1;

    /// <summary>Gets or sets the note describing the latest definition change.</summary>
    public string? VersionNote { get; set; }
}
