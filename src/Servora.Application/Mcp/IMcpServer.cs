namespace Servora.Application.Mcp;

/// <summary>Transport-independent JSON-RPC 2.0 dispatcher of the Model Context Protocol.</summary>
public interface IMcpServer
{
    /// <summary>Handles one HTTP request body (a single JSON-RPC message or a batch).</summary>
    /// <param name="body">The raw JSON body.</param>
    /// <param name="context">The call context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JSON response, or <see langword="null"/> when the body only contained notifications or responses.</returns>
    Task<string?> HandleAsync(string body, McpCallContext context, CancellationToken cancellationToken = default);
}
