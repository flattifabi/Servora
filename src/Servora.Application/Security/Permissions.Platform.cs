using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Platform &amp; Studio module.</summary>
    [Description("Platform & Studio")]
    public static class Platform
    {
        /// <summary>Use the generic table list and form views (table ACLs still apply).</summary>
        [Description("Use the generic table list and form views (table ACLs still apply).")]
        public const string ReadRecords = "platform.records.read";

        /// <summary>Create custom tables and fields, edit forms and lists.</summary>
        [Description("Create custom tables and fields, edit forms and lists.")]
        public const string ManageTables = "platform.tables.manage";

        /// <summary>Manage business rules.</summary>
        [Description("Manage business rules.")]
        public const string ManageBusinessRules = "platform.businessrules.manage";

        /// <summary>Manage SLA definitions.</summary>
        [Description("Manage SLA definitions.")]
        public const string ManageSla = "platform.sla.manage";

        /// <summary>Manage notifications and e-mail templates.</summary>
        [Description("Manage notifications and e-mail templates.")]
        public const string ManageNotifications = "platform.notifications.manage";
    }
}
