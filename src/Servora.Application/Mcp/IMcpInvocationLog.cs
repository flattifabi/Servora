namespace Servora.Application.Mcp;

/// <summary>Persists MCP invocations for the admin page and writes the per-call audit event.</summary>
public interface IMcpInvocationLog
{
    /// <summary>Records an invocation.</summary>
    /// <param name="record">The invocation.</param>
    /// <param name="context">The call context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the invocation is stored.</returns>
    Task RecordAsync(McpInvocationRecord record, McpCallContext context, CancellationToken cancellationToken = default);
}
