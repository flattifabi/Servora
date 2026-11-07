namespace Servora.Domain.Analytics;

/// <summary>Bucket size used when a report is grouped by a date / time field.</summary>
public enum ReportDateInterval
{
    /// <summary>One bucket per day.</summary>
    Day = 0,

    /// <summary>One bucket per ISO week (starting Monday).</summary>
    Week = 1,

    /// <summary>One bucket per month.</summary>
    Month = 2,

    /// <summary>One bucket per quarter.</summary>
    Quarter = 3,

    /// <summary>One bucket per year.</summary>
    Year = 4,
}
