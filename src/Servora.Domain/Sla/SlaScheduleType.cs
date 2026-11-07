namespace Servora.Domain.Sla;

/// <summary>Working time model used to measure SLA durations.</summary>
public enum SlaScheduleType
{
    /// <summary>The clock runs around the clock, every day.</summary>
    TwentyFourSeven = 0,

    /// <summary>The clock only runs during business hours on business days in the definition's time zone.</summary>
    BusinessHours = 1,
}
