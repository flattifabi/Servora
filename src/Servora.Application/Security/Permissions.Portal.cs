using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Self-Service Portal module.</summary>
    [Description("Self-Service Portal")]
    public static class Portal
    {
        /// <summary>Use the self-service portal (submit requests and issues, read knowledge).</summary>
        [Description("Use the self-service portal (submit requests and issues, read knowledge).")]
        public const string Access = "portal.access";

        /// <summary>Manage the portal announcements and the portal settings (texts, links, hero image).</summary>
        [Description("Manage the portal announcements and the portal settings (texts, links, hero image).")]
        public const string Manage = "portal.manage";
    }
}
