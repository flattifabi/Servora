using Servora.Domain.Mcp;

namespace Servora.Application.Mcp;

/// <summary>List row of an MCP invocation.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="OccurredAt">The UTC time.</param>
/// <param name="Method">The JSON-RPC method.</param>
/// <param name="Target">The tool, resource or client.</param>
/// <param name="ActorName">The caller.</param>
/// <param name="ClientName">The MCP client name.</param>
/// <param name="Outcome">The outcome.</param>
/// <param name="DurationMs">The duration.</param>
/// <param name="ErrorMessage">The error message.</param>
/// <param name="IpAddress">The client address.</param>
public sealed record McpInvocationRow(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Method,
    string? Target,
    string? ActorName,
    string? ClientName,
    McpInvocationOutcome Outcome,
    long DurationMs,
    string? ErrorMessage,
    string? IpAddress);
