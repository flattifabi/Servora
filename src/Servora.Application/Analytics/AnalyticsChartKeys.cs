namespace Servora.Application.Analytics;

/// <summary>Keys of the executive dashboard charts.</summary>
public static class AnalyticsChartKeys
{
    /// <summary>Incidents opened vs resolved per day.</summary>
    public const string IncidentTrend = "incidents.trend";

    /// <summary>Open incident backlog by priority.</summary>
    public const string IncidentBacklog = "incidents.backlog";

    /// <summary>Mean time to resolve per week (hours).</summary>
    public const string Mttr = "incidents.mttr";

    /// <summary>Top incident categories in the period.</summary>
    public const string TopCategories = "incidents.categories";

    /// <summary>SLA compliance (met vs breached).</summary>
    public const string SlaCompliance = "sla.compliance";

    /// <summary>Change requests by state.</summary>
    public const string ChangesByState = "changes.state";

    /// <summary>Requested items by catalog item.</summary>
    public const string RequestsByItem = "requests.items";

    /// <summary>Automation (flow run) outcomes.</summary>
    public const string AutomationSuccess = "automation.success";

    /// <summary>Configuration items by class.</summary>
    public const string CisByClass = "cmdb.classes";

    /// <summary>Audit activity per day (success / failure / denied).</summary>
    public const string AuditActivity = "audit.activity";

    /// <summary>Sign-ins per day.</summary>
    public const string SignIns = "platform.signins";

    /// <summary>Users by source (local, directory, Windows).</summary>
    public const string UsersBySource = "users.sources";
}
