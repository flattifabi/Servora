using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Domain.Configuration;
using Servora.Domain.Mcp;

namespace Servora.Application.Mcp;

/// <summary>Default <see cref="IMcpRuntimeStateProvider"/> (cached for 10 seconds, invalidated on changes).</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="cache">The memory cache.</param>
public sealed class McpRuntimeStateProvider(IAppDbContextFactory dbFactory, IMemoryCache cache) : IMcpRuntimeStateProvider
{
    /// <summary>The system property that holds the server enable switch.</summary>
    public const string EnabledSettingKey = "mcp.server.enabled";

    private const string CacheKey = "servora:mcp:state";

    /// <inheritdoc />
    public async Task<McpRuntimeState> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out McpRuntimeState? cached) && cached is not null)
        {
            return cached;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var enabledValue = await db.Set<SystemSetting>().AsNoTracking()
            .Where(s => s.Key == EnabledSettingKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        var disabled = await db.Set<McpToolSetting>().AsNoTracking()
            .Where(t => !t.IsEnabled).Select(t => t.ToolName).Take(500).ToListAsync(cancellationToken);
        var state = new McpRuntimeState(
            !string.Equals(enabledValue, "false", StringComparison.OrdinalIgnoreCase),
            disabled.ToHashSet(StringComparer.Ordinal));
        cache.Set(CacheKey, state, TimeSpan.FromSeconds(10));
        return state;
    }

    /// <inheritdoc />
    public void Invalidate() => cache.Remove(CacheKey);
}
