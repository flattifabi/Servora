using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Incident Management module.</summary>
    [Description("Incident Management")]
    public static class Incidents
    {
        /// <summary>View incidents.</summary>
        [Description("View incidents.")]
        public const string Read = "incident.read";

        /// <summary>Create incidents.</summary>
        [Description("Create incidents.")]
        public const string Create = "incident.create";

        /// <summary>Update, assign and resolve incidents.</summary>
        [Description("Update, assign and resolve incidents.")]
        public const string Write = "incident.write";

        /// <summary>Delete incidents.</summary>
        [Description("Delete incidents.")]
        public const string Delete = "incident.delete";
    }
}
