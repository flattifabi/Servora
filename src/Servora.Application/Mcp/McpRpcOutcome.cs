using System.Text.Json.Nodes;

namespace Servora.Application.Mcp;

/// <summary>Result of dispatching one JSON-RPC method: either a result node or an error.</summary>
/// <param name="Result">The result (for successful requests).</param>
/// <param name="ErrorCode">The JSON-RPC error code.</param>
/// <param name="ErrorMessage">The error message.</param>
/// <param name="ErrorData">Optional structured error data.</param>
public sealed record McpRpcOutcome(JsonNode? Result, int? ErrorCode = null, string? ErrorMessage = null, JsonNode? ErrorData = null)
{
    /// <summary>Gets a value indicating whether the outcome is an error.</summary>
    public bool IsError => ErrorCode is not null;

    /// <summary>Creates a successful outcome.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The outcome.</returns>
    public static McpRpcOutcome Ok(JsonNode result) => new(result);

    /// <summary>Creates an error outcome.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The message.</param>
    /// <param name="data">Optional data.</param>
    /// <returns>The outcome.</returns>
    public static McpRpcOutcome Error(int code, string message, JsonNode? data = null) => new(null, code, message, data);
}
