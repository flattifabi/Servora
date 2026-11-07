namespace Servora.Domain.Analytics;

/// <summary>Who can see a saved report.</summary>
public enum ReportVisibility
{
    /// <summary>Only the owner.</summary>
    Private = 0,

    /// <summary>The owner and every user holding <see cref="ReportDefinition.SharedWithRole"/>.</summary>
    Role = 1,

    /// <summary>Every user allowed to use analytics.</summary>
    Everyone = 2,
}
