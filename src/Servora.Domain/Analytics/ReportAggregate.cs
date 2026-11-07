namespace Servora.Domain.Analytics;

/// <summary>Aggregate function applied to the records of each report group.</summary>
public enum ReportAggregate
{
    /// <summary>Number of records.</summary>
    Count = 0,

    /// <summary>Sum of a numeric field.</summary>
    Sum = 1,

    /// <summary>Average of a numeric field.</summary>
    Average = 2,

    /// <summary>Minimum of a numeric field.</summary>
    Min = 3,

    /// <summary>Maximum of a numeric field.</summary>
    Max = 4,
}
