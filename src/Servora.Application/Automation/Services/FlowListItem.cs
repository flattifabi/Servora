using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>A row of the flow list.</summary>
/// <param name="Id">The flow identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="IsActive">Whether the flow is active.</param>
/// <param name="TriggerType">The trigger type.</param>
/// <param name="TriggerTable">The trigger table for record triggers.</param>
/// <param name="CronExpression">The cron expression for time-based triggers.</param>
/// <param name="DefinitionVersion">The definition version.</param>
/// <param name="UpdatedAt">The last modification time.</param>
/// <param name="UpdatedBy">The last modifier.</param>
/// <param name="NextRunAt">The next scheduled fire time.</param>
public sealed record FlowListItem(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    FlowTriggerType TriggerType,
    string? TriggerTable,
    string? CronExpression,
    int DefinitionVersion,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? NextRunAt)
{
    /// <summary>Gets or sets the start time of the latest run.</summary>
    public DateTimeOffset? LastRunAt { get; init; }

    /// <summary>Gets or sets the status of the latest run.</summary>
    public FlowRunStatus? LastRunStatus { get; init; }

    /// <summary>Gets or sets the number of runs in the last 24 hours.</summary>
    public int Runs24h { get; init; }
}
