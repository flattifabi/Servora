namespace Servora.Application.Mcp;

/// <summary>Provides the cached MCP enable switches (server and per tool). Contains no secrets, so no permission is required.</summary>
public interface IMcpRuntimeStateProvider
{
    /// <summary>Gets the current state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state.</returns>
    Task<McpRuntimeState> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the cached state after a configuration change.</summary>
    void Invalidate();
}
