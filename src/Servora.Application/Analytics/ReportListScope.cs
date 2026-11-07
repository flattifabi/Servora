namespace Servora.Application.Analytics;

/// <summary>Which saved reports to list.</summary>
public enum ReportListScope
{
    /// <summary>Every report visible to the user.</summary>
    All = 0,

    /// <summary>Reports owned by the user.</summary>
    Mine = 1,

    /// <summary>Reports shared with the user by others (role or everyone) and built-in samples.</summary>
    Shared = 2,
}
