namespace Servora.Domain.Mcp;

/// <summary>Outcome of an MCP request.</summary>
public enum McpInvocationOutcome
{
    /// <summary>The request succeeded.</summary>
    Success = 0,

    /// <summary>The tool ran but reported an error (for example validation or not found).</summary>
    ToolError = 1,

    /// <summary>The caller lacked the tool scope or the underlying permission.</summary>
    Denied = 2,

    /// <summary>The arguments did not match the tool's input schema.</summary>
    InvalidParams = 3,

    /// <summary>An unexpected server error occurred.</summary>
    Error = 4,
}
