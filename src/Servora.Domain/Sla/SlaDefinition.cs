using Servora.Domain.Common;

namespace Servora.Domain.Sla;

/// <summary>
/// Service level definition: which records of a table get a timer (start condition), when the timer pauses and stops,
/// the target duration and the working-time schedule used to measure it.
/// </summary>
public class SlaDefinition : AuditableEntity
{
    /// <summary>Gets or sets the name, for example <c>P1 resolution</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether new timers are attached.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the target duration in minutes (measured in schedule time).</summary>
    public int TargetMinutes { get; set; } = 480;

    /// <summary>Gets or sets the start condition JSON; empty starts a timer for every record.</summary>
    public string? StartConditionJson { get; set; }

    /// <summary>Gets or sets the pause condition JSON; empty never pauses.</summary>
    public string? PauseConditionJson { get; set; }

    /// <summary>Gets or sets the stop (achieved) condition JSON; empty never stops automatically.</summary>
    public string? StopConditionJson { get; set; }

    /// <summary>Gets or sets the cancel condition JSON; empty never cancels automatically.</summary>
    public string? CancelConditionJson { get; set; }

    /// <summary>Gets or sets the schedule type.</summary>
    public SlaScheduleType ScheduleType { get; set; } = SlaScheduleType.TwentyFourSeven;

    /// <summary>Gets or sets the IANA or Windows time zone identifier of the business-hours schedule.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Gets or sets the business days as comma separated <see cref="DayOfWeek"/> numbers (0 = Sunday), for example <c>1,2,3,4,5</c>.</summary>
    public string BusinessDays { get; set; } = "1,2,3,4,5";

    /// <summary>Gets or sets the start of the business day in minutes after midnight (local time).</summary>
    public int DayStartMinute { get; set; } = 8 * 60;

    /// <summary>Gets or sets the end of the business day in minutes after midnight (local time).</summary>
    public int DayEndMinute { get; set; } = 17 * 60;

    /// <summary>Gets or sets the notification sent when a timer breaches (optional).</summary>
    public Guid? BreachNotificationId { get; set; }
}
