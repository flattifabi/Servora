using Servora.Application.Mcp;

namespace Servora.Tests.Unit.Mcp.Fakes;

/// <summary><see cref="IMcpRuntimeStateProvider"/> with settable switches.</summary>
internal sealed class StaticMcpRuntimeStateProvider : IMcpRuntimeStateProvider
{
    /// <summary>Gets or sets a value indicating whether the server is enabled.</summary>
    public bool ServerEnabled { get; set; } = true;

    /// <summary>Gets the disabled tool names.</summary>
    public HashSet<string> DisabledTools { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<McpRuntimeState> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new McpRuntimeState(ServerEnabled, DisabledTools.ToHashSet(StringComparer.Ordinal)));

    /// <inheritdoc />
    public void Invalidate()
    {
    }
}
