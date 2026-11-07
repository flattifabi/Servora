namespace Servora.Application.Mcp;

/// <summary>Data of the MCP administration page.</summary>
/// <param name="ServerEnabled">Whether the server accepts requests.</param>
/// <param name="Tools">The tool catalog.</param>
/// <param name="Calls24h">Tool calls in the last 24 hours.</param>
/// <param name="Failed24h">Failed tool calls (tool errors, invalid params, internal errors) in the last 24 hours.</param>
/// <param name="Denied24h">Denied tool calls in the last 24 hours.</param>
/// <param name="Clients24h">Distinct API keys that called the server in the last 24 hours.</param>
/// <param name="Hourly">Tool calls per hour for the last 24 hours, oldest first.</param>
public sealed record McpOverview(
    bool ServerEnabled,
    IReadOnlyList<McpToolInfo> Tools,
    int Calls24h,
    int Failed24h,
    int Denied24h,
    int Clients24h,
    IReadOnlyList<double> Hourly);
