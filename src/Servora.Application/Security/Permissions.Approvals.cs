using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Approvals module.</summary>
    [Description("Approvals")]
    public static class Approvals
    {
        /// <summary>View all approvals (own approvals are always visible).</summary>
        [Description("View all approvals (own approvals are always visible).")]
        public const string Read = "approval.read";

        /// <summary>Reassign, cancel and administer approvals.</summary>
        [Description("Reassign, cancel and administer approvals.")]
        public const string Manage = "approval.manage";
    }
}
