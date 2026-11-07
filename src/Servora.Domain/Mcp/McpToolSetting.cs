using Servora.Domain.Common;

namespace Servora.Domain.Mcp;

/// <summary>Administrator setting that enables or disables one MCP tool. Tools without a setting are enabled.</summary>
public class McpToolSetting : AuditableEntity
{
    /// <summary>Gets or sets the tool name, for example <c>search_records</c>.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the tool is exposed by the MCP server.</summary>
    public bool IsEnabled { get; set; } = true;
}
