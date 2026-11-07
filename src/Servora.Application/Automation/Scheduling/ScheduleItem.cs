namespace Servora.Application.Automation.Scheduling;

/// <summary>A scheduled job (flow schedule, data source poll or import).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Id">The flow or import set identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="CronExpression">The cron expression.</param>
/// <param name="TimeZoneId">The time zone (UTC when empty).</param>
/// <param name="Enabled">Whether the schedule is active.</param>
/// <param name="NextRunAt">The next fire time.</param>
/// <param name="LastRunAt">The last fire time.</param>
/// <param name="Link">The UI link of the owning object.</param>
public sealed record ScheduleItem(
    ScheduleKind Kind,
    Guid Id,
    string Name,
    string? CronExpression,
    string? TimeZoneId,
    bool Enabled,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? LastRunAt,
    string Link);
