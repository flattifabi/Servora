using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Backup &amp; Restore module. Only the built-in admin role holds them by default.</summary>
    [Description("Backup & Restore")]
    public static class Backup
    {
        /// <summary>View backups, the backup schedule and the database schema state.</summary>
        [Description("View backups, the backup schedule and the database schema state.")]
        public const string Read = "backup.read";

        /// <summary>Create, upload, download and delete backups and change the backup schedule.</summary>
        [Description("Create, upload, download and delete backups and change the backup schedule.")]
        public const string Manage = "backup.manage";

        /// <summary>Restore (roll back) the whole instance to a backup.</summary>
        [Description("Restore (roll back) the whole instance to a backup.")]
        public const string Restore = "backup.restore";
    }
}
