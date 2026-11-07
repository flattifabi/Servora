namespace Servora.Application.Automation.Scheduling;

/// <summary>Kind of a scheduled job.</summary>
public enum ScheduleKind
{
    /// <summary>A flow with a schedule trigger.</summary>
    Flow = 0,

    /// <summary>A flow with a data source poll trigger.</summary>
    DataSourcePoll = 1,

    /// <summary>A scheduled import set.</summary>
    Import = 2,
}
