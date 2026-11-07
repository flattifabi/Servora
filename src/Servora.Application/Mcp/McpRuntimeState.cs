namespace Servora.Application.Mcp;

/// <summary>Effective MCP configuration used while serving requests.</summary>
/// <param name="ServerEnabled">Whether the MCP server accepts requests.</param>
/// <param name="DisabledTools">The names of tools disabled by an administrator.</param>
public sealed record McpRuntimeState(bool ServerEnabled, IReadOnlySet<string> DisabledTools)
{
    /// <summary>Checks whether a tool is enabled.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns><see langword="true"/> when enabled.</returns>
    public bool IsToolEnabled(string toolName) => !DisabledTools.Contains(toolName);
}
