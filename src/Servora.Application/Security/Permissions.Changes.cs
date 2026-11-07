using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Change Management module.</summary>
    [Description("Change Management")]
    public static class Changes
    {
        /// <summary>View change requests.</summary>
        [Description("View change requests.")]
        public const string Read = "change.read";

        /// <summary>Create change requests.</summary>
        [Description("Create change requests.")]
        public const string Create = "change.create";

        /// <summary>Update, schedule and implement change requests.</summary>
        [Description("Update, schedule and implement change requests.")]
        public const string Write = "change.write";

        /// <summary>Approve change requests as change manager / CAB.</summary>
        [Description("Approve change requests as change manager / CAB.")]
        public const string Approve = "change.approve";

        /// <summary>Delete change requests.</summary>
        [Description("Delete change requests.")]
        public const string Delete = "change.delete";
    }
}
