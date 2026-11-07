using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Analytics &amp; Reporting module.</summary>
    [Description("Analytics & Reporting")]
    public static class Analytics
    {
        /// <summary>View dashboards and reports.</summary>
        [Description("View dashboards and reports.")]
        public const string Read = "analytics.read";

        /// <summary>Create, edit and share reports and dashboards.</summary>
        [Description("Create, edit and share reports and dashboards.")]
        public const string ManageReports = "analytics.reports.manage";
    }
}
