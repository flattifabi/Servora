using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Configuration module.</summary>
    [Description("Configuration")]
    public static class Configuration
    {
        /// <summary>Manage system properties, branding and e-mail settings.</summary>
        [Description("Manage system properties, branding and e-mail settings.")]
        public const string ManageSettings = "admin.settings.manage";

        /// <summary>Create, rotate and delete stored secrets.</summary>
        [Description("Create, rotate and delete stored secrets.")]
        public const string ManageSecrets = "admin.secrets.manage";

        /// <summary>View database information and test connections.</summary>
        [Description("View database information and test connections.")]
        public const string ManageDatabase = "admin.database.manage";
    }
}
