using Servora.Domain.Mcp;

namespace Servora.Application.Mcp;

/// <summary>Data of one MCP request to log and audit (never contains tool arguments or results).</summary>
/// <param name="Method">The JSON-RPC method.</param>
/// <param name="Target">The tool name, resource URI or client name.</param>
/// <param name="Outcome">The outcome.</param>
/// <param name="DurationMs">The duration in milliseconds.</param>
/// <param name="ErrorMessage">A user-safe error message.</param>
public sealed record McpInvocationRecord(string Method, string? Target, McpInvocationOutcome Outcome, long DurationMs, string? ErrorMessage = null)
{
    /// <summary>Gets the client name reported by <c>initialize</c>.</summary>
    public string? ClientName { get; init; }
}
