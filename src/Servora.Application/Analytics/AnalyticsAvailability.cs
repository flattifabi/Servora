namespace Servora.Application.Analytics;

/// <summary>Whether a dashboard widget could be computed.</summary>
public enum AnalyticsAvailability
{
    /// <summary>Data is available.</summary>
    Ready = 0,

    /// <summary>The source exists but holds no data for the period.</summary>
    NoData = 1,

    /// <summary>The module providing the data is not installed.</summary>
    ModuleMissing = 2,

    /// <summary>The current user may not see the underlying data.</summary>
    Restricted = 3,
}
