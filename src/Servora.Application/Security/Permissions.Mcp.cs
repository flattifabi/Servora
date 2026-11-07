using System.ComponentModel;

namespace Servora.Application.Security;

public static partial class Permissions
{
    /// <summary>Permissions of the MCP Server module.</summary>
    [Description("MCP Server")]
    public static class Mcp
    {
        /// <summary>Connect to the MCP server.</summary>
        [Description("Connect to the MCP server.")]
        public const string Access = "mcp.access";

        /// <summary>Configure the MCP server and exposed tools.</summary>
        [Description("Configure the MCP server and exposed tools.")]
        public const string Manage = "admin.mcp.manage";
    }
}
