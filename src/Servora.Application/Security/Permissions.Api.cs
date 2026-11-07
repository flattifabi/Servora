using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the API &amp; API Keys module.</summary>
    [Description("API & API Keys")]
    public static class Api
    {
        /// <summary>Call the REST API (granted to API keys and users).</summary>
        [Description("Call the REST API (granted to API keys and users).")]
        public const string Access = "api.access";

        /// <summary>Create, rotate and revoke API keys.</summary>
        [Description("Create, rotate and revoke API keys.")]
        public const string ManageApiKeys = "admin.apikeys.manage";
    }
}
