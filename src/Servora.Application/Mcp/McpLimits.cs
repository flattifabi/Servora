namespace Servora.Application.Mcp;

/// <summary>Defensive limits of the MCP server.</summary>
public static class McpLimits
{
    /// <summary>The maximum request body size in bytes.</summary>
    public const int MaxRequestBytes = 1024 * 1024;

    /// <summary>The maximum number of messages in a JSON-RPC batch.</summary>
    public const int MaxBatchSize = 20;

    /// <summary>The maximum length of a tool result text (characters); longer results are truncated.</summary>
    public const int MaxOutputChars = 64 * 1024;

    /// <summary>The maximum number of records returned by list/search tools.</summary>
    public const int MaxRecords = 50;

    /// <summary>The maximum JSON nesting depth of requests.</summary>
    public const int MaxJsonDepth = 32;

    /// <summary>The timeout of a single tool execution.</summary>
    public static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(30);
}
