using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Observability module.</summary>
    [Description("Observability")]
    public static class Observability
    {
        /// <summary>View application logs.</summary>
        [Description("View application logs.")]
        public const string ReadLogs = "admin.logs.read";

        /// <summary>Change log levels (debug switch) and retention.</summary>
        [Description("Change log levels (debug switch) and retention.")]
        public const string ManageLogs = "admin.logs.manage";

        /// <summary>View and export the audit log.</summary>
        [Description("View and export the audit log.")]
        public const string ReadAudit = "admin.audit.read";

        /// <summary>View system health and metrics.</summary>
        [Description("View system health and metrics.")]
        public const string ReadHealth = "admin.health.read";
    }
}
