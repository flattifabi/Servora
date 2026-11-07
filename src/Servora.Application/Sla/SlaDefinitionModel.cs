using Servora.Application.Conditions;
using Servora.Domain.Sla;

namespace Servora.Application.Sla;

/// <summary>An SLA definition as edited in the UI.</summary>
public sealed class SlaDefinitionModel
{
    /// <summary>Gets or sets the identifier (empty for new definitions).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether new timers are attached.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the target duration in minutes.</summary>
    public int TargetMinutes { get; set; } = 480;

    /// <summary>Gets or sets the start condition (empty = every record).</summary>
    public ConditionGroup StartCondition { get; set; } = new();

    /// <summary>Gets or sets the pause condition (empty = never).</summary>
    public ConditionGroup PauseCondition { get; set; } = new();

    /// <summary>Gets or sets the stop condition (empty = never).</summary>
    public ConditionGroup StopCondition { get; set; } = new();

    /// <summary>Gets or sets the cancel condition (empty = never).</summary>
    public ConditionGroup CancelCondition { get; set; } = new();

    /// <summary>Gets or sets the schedule type.</summary>
    public SlaScheduleType ScheduleType { get; set; } = SlaScheduleType.TwentyFourSeven;

    /// <summary>Gets or sets the time zone of business hours.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Gets or sets the business days.</summary>
    public List<DayOfWeek> BusinessDays { get; set; } = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>Gets or sets the start of the business day in minutes after midnight.</summary>
    public int DayStartMinute { get; set; } = 8 * 60;

    /// <summary>Gets or sets the end of the business day in minutes after midnight.</summary>
    public int DayEndMinute { get; set; } = 17 * 60;

    /// <summary>Gets or sets the notification sent on breach.</summary>
    public Guid? BreachNotificationId { get; set; }

    /// <summary>Gets or sets the number of running timers (read only).</summary>
    public int ActiveTimers { get; set; }

    /// <summary>Gets or sets the UTC time of the last change (read only).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
