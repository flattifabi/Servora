using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Configuration Management (CMDB) module.</summary>
    [Description("Configuration Management (CMDB)")]
    public static class Cmdb
    {
        /// <summary>View configuration items and relationships.</summary>
        [Description("View configuration items and relationships.")]
        public const string Read = "cmdb.read";

        /// <summary>Create and update configuration items and relationships.</summary>
        [Description("Create and update configuration items and relationships.")]
        public const string Write = "cmdb.write";

        /// <summary>Manage CI classes and delete configuration items.</summary>
        [Description("Manage CI classes and delete configuration items.")]
        public const string Manage = "cmdb.manage";
    }
}
