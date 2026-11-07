using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the Asset Management module.</summary>
    [Description("Asset Management")]
    public static class Assets
    {
        /// <summary>View assets.</summary>
        [Description("View assets.")]
        public const string Read = "asset.read";

        /// <summary>Create, update and retire assets.</summary>
        [Description("Create, update and retire assets.")]
        public const string Write = "asset.write";

        /// <summary>Manage asset models, contracts and delete assets.</summary>
        [Description("Manage asset models, contracts and delete assets.")]
        public const string Manage = "asset.manage";
    }
}
