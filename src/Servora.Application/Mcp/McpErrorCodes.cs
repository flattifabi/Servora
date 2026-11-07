namespace Servora.Application.Mcp;

/// <summary>JSON-RPC 2.0 error codes used by the MCP server.</summary>
public static class McpErrorCodes
{
    /// <summary>Invalid JSON was received.</summary>
    public const int ParseError = -32700;

    /// <summary>The JSON is not a valid request object.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>The method does not exist.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Invalid method parameters (unknown tool, schema violations).</summary>
    public const int InvalidParams = -32602;

    /// <summary>Internal error.</summary>
    public const int InternalError = -32603;

    /// <summary>Server-defined: the MCP server is disabled by the administrator.</summary>
    public const int ServerDisabled = -32000;

    /// <summary>Server-defined: the caller lacks the tool scope or the underlying permission.</summary>
    public const int Forbidden = -32001;

    /// <summary>Resource not found (MCP convention).</summary>
    public const int ResourceNotFound = -32002;
}
