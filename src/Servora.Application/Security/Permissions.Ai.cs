using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the AI &amp; Agents module.</summary>
    [Description("AI & Agents")]
    public static class Ai
    {
        /// <summary>Use the AI assistant.</summary>
        [Description("Use the AI assistant.")]
        public const string UseAssistant = "ai.assistant.use";

        /// <summary>Configure AI providers, agents and their permissions.</summary>
        [Description("Configure AI providers, agents and their permissions.")]
        public const string Manage = "admin.ai.manage";
    }
}
