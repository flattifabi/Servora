using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Mcp;

namespace Servora.Application.Mcp;

/// <summary>Default <see cref="IMcpSettingsService"/>.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="tools">The registered tools.</param>
/// <param name="state">The runtime state provider (cache invalidation).</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class McpSettingsService(
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IEnumerable<IMcpTool> tools,
    IMcpRuntimeStateProvider state,
    IAuditService audit,
    IClock clock) : IMcpSettingsService
{
    private static readonly JsonSerializerOptions s_pretty = new() { WriteIndented = true };

    /// <inheritdoc />
    public async Task<Result<McpOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Mcp.Manage, cancellationToken))
        {
            return Result<McpOverview>.Forbidden();
        }

        state.Invalidate();
        var runtime = await state.GetAsync(cancellationToken);
        var now = clock.UtcNow;
        var since = now.AddHours(-24);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var calls = await db.Set<McpInvocation>().AsNoTracking()
            .Where(i => i.OccurredAt >= since && i.Method == "tools/call")
            .OrderByDescending(i => i.OccurredAt)
            .Take(20_000)
            .Select(i => new { i.OccurredAt, i.Target, i.Outcome, i.ApiKeyId })
            .ToListAsync(cancellationToken);
        var clients = await db.Set<McpInvocation>().AsNoTracking()
            .Where(i => i.OccurredAt >= since && i.ApiKeyId != null)
            .Select(i => i.ApiKeyId).Distinct().CountAsync(cancellationToken);

        var toolInfos = tools.OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => new McpToolInfo(
            t.Name,
            t.Title,
            t.Description,
            McpScopes.ForTool(t.Name),
            t.RequiredPermission,
            t.IsReadOnly,
            t.IsConsequential,
            runtime.IsToolEnabled(t.Name),
            t.CreateInputSchema().ToJsonString(s_pretty),
            calls.Count(c => c.Target == t.Name))).ToList();

        var hourly = Enumerable.Range(0, 24)
            .Select(h => (double)calls.Count(c => c.OccurredAt >= since.AddHours(h) && c.OccurredAt < since.AddHours(h + 1)))
            .ToList();

        return Result<McpOverview>.Success(new McpOverview(
            runtime.ServerEnabled,
            toolInfos,
            calls.Count,
            calls.Count(c => c.Outcome is McpInvocationOutcome.ToolError or McpInvocationOutcome.InvalidParams or McpInvocationOutcome.Error),
            calls.Count(c => c.Outcome == McpInvocationOutcome.Denied),
            clients,
            hourly));
    }

    /// <inheritdoc />
    public async Task<Result> SetServerEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Mcp.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.Set<SystemSetting>().FirstOrDefaultAsync(s => s.Key == McpRuntimeStateProvider.EnabledSettingKey, cancellationToken);
        if (setting is null)
        {
            setting = new SystemSetting
            {
                Key = McpRuntimeStateProvider.EnabledSettingKey,
                Category = "MCP Server",
                ValueType = "bool",
                Description = "Accept Model Context Protocol requests at /mcp.",
            };
            db.Set<SystemSetting>().Add(setting);
        }

        setting.Value = enabled ? "true" : "false";
        await db.SaveChangesAsync(cancellationToken);
        state.Invalidate();
        await audit.WriteAsync(new AuditEntry("config", enabled ? "mcp.server.enabled" : "mcp.server.disabled")
        {
            TargetType = "mcp_server",
            TargetName = "MCP server",
            Message = enabled ? "MCP server enabled." : "MCP server disabled.",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetToolEnabledAsync(string toolName, bool enabled, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Mcp.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (!tools.Any(t => t.Name == toolName))
        {
            return Result.NotFound("The tool does not exist.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.Set<McpToolSetting>().FirstOrDefaultAsync(t => t.ToolName == toolName, cancellationToken);
        if (setting is null)
        {
            setting = new McpToolSetting { ToolName = toolName };
            db.Set<McpToolSetting>().Add(setting);
        }

        setting.IsEnabled = enabled;
        await db.SaveChangesAsync(cancellationToken);
        state.Invalidate();
        await audit.WriteAsync(new AuditEntry("config", enabled ? "mcp.tool.enabled" : "mcp.tool.disabled")
        {
            TargetType = "mcp_tool",
            TargetName = toolName,
            Message = $"MCP tool '{toolName}' {(enabled ? "enabled" : "disabled")}.",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<McpInvocationRow>>> ListInvocationsAsync(PageRequest page, McpInvocationOutcome? outcome = null, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Mcp.Manage, cancellationToken))
        {
            return Result<PagedResult<McpInvocationRow>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<McpInvocation>().AsNoTracking();
        if (outcome is { } filter)
        {
            query = query.Where(i => i.Outcome == filter);
        }

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(i => (i.Target != null && i.Target.Contains(term)) || (i.ActorName != null && i.ActorName.Contains(term))
                                     || (i.ClientName != null && i.ClientName.Contains(term)) || i.Method.Contains(term));
        }

        var result = await query
            .OrderByDescending(i => i.OccurredAt)
            .Select(i => new McpInvocationRow(i.Id, i.OccurredAt, i.Method, i.Target, i.ActorName, i.ClientName, i.Outcome, i.DurationMs, i.ErrorMessage, i.IpAddress))
            .ToPagedResultAsync(page, cancellationToken);
        return Result<PagedResult<McpInvocationRow>>.Success(result);
    }
}
