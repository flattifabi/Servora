using Servora.Application.Common;
using Servora.Domain.Mcp;

namespace Servora.Application.Mcp;

/// <summary>Administration of the MCP server (requires <c>admin.mcp.manage</c>).</summary>
public interface IMcpSettingsService
{
    /// <summary>Gets the server switch, tool catalog and KPIs.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The overview.</returns>
    Task<Result<McpOverview>> GetOverviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Enables or disables the MCP server.</summary>
    /// <param name="enabled">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetServerEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables one tool.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="enabled">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetToolEnabledAsync(string toolName, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Lists recent invocations.</summary>
    /// <param name="page">Paging and search (tool, actor, client).</param>
    /// <param name="outcome">An optional outcome filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of invocations, newest first.</returns>
    Task<Result<PagedResult<McpInvocationRow>>> ListInvocationsAsync(PageRequest page, McpInvocationOutcome? outcome = null, CancellationToken cancellationToken = default);
}
