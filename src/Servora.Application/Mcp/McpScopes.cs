namespace Servora.Application.Mcp;

/// <summary>Naming convention of per-tool MCP scopes (<c>mcp.tool.&lt;name&gt;</c>).</summary>
public static class McpScopes
{
    /// <summary>The scope prefix of MCP tools.</summary>
    public const string ToolPrefix = "mcp.tool.";

    /// <summary>The scope that grants every MCP tool (the underlying permissions still apply).</summary>
    public const string AllTools = "mcp.tool.*";

    /// <summary>Builds the scope of a tool.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>The scope key.</returns>
    public static string ForTool(string toolName) => ToolPrefix + toolName;
}
