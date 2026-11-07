using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Automation module.</summary>
    [Description("Automation")]
    public static class Automation
    {
        /// <summary>View flows, schedules and execution history.</summary>
        [Description("View flows, schedules and execution history.")]
        public const string Read = "automation.read";

        /// <summary>Create, edit, activate and delete flows and schedules.</summary>
        [Description("Create, edit, activate and delete flows and schedules.")]
        public const string Manage = "automation.manage";

        /// <summary>Start flows manually.</summary>
        [Description("Start flows manually.")]
        public const string Execute = "automation.execute";
    }
}
