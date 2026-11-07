using Servora.Application.Automation.Model;
using Servora.Application.Conditions;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>The editable definition of a flow as used by the Flow Designer (read and save model).</summary>
public sealed class FlowDefinitionModel
{
    /// <summary>Gets or sets the flow identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets a value indicating whether the flow is active (read-only here; use activation).</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the trigger type.</summary>
    public FlowTriggerType TriggerType { get; set; }

    /// <summary>Gets or sets the trigger table.</summary>
    public string? TriggerTable { get; set; }

    /// <summary>Gets or sets the trigger condition.</summary>
    public ConditionGroup TriggerCondition { get; set; } = new();

    /// <summary>Gets or sets the cron expression.</summary>
    public string? CronExpression { get; set; }

    /// <summary>Gets or sets the time zone identifier.</summary>
    public string? TimeZoneId { get; set; }

    /// <summary>Gets or sets the polled data source.</summary>
    public Guid? PollDataSourceId { get; set; }

    /// <summary>Gets or sets the polled endpoint key.</summary>
    public string? PollEndpointKey { get; set; }

    /// <summary>Gets or sets the poll parameter values.</summary>
    public Dictionary<string, string> PollParameters { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the poll trigger fires only on changed responses.</summary>
    public bool PollOnlyOnChange { get; set; } = true;

    /// <summary>Gets or sets the inputs.</summary>
    public List<FlowInputDefinition> Inputs { get; set; } = [];

    /// <summary>Gets or sets the step tree.</summary>
    public List<FlowStepDefinition> Steps { get; set; } = [];

    /// <summary>Gets or sets the definition version.</summary>
    public int DefinitionVersion { get; set; }

    /// <summary>Gets or sets the latest version note.</summary>
    public string? VersionNote { get; set; }

    /// <summary>Gets or sets the concurrency token loaded with the flow.</summary>
    public long ConcurrencyVersion { get; set; }

    /// <summary>Gets or sets the next scheduled fire time.</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>Gets or sets the last trigger time.</summary>
    public DateTimeOffset? LastTriggeredAt { get; set; }

    /// <summary>Gets or sets the last modification time.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the last modifier.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Gets or sets a value indicating whether a webhook secret exists.</summary>
    public bool HasWebhookSecret { get; set; }

    /// <summary>Gets or sets the relative webhook URL.</summary>
    public string WebhookPath => $"/api/v1/automation/webhooks/{Id}";
}
