using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Domain.Mcp;
using Servora.Domain.Observability;

namespace Servora.Application.Mcp;

/// <summary>Default <see cref="IMcpInvocationLog"/>: one <see cref="McpInvocation"/> row plus an audit event per call.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class McpInvocationLog(IAppDbContextFactory dbFactory, IAuditService audit, IClock clock, ILogger<McpInvocationLog> logger) : IMcpInvocationLog
{
    /// <inheritdoc />
    public async Task RecordAsync(McpInvocationRecord record, McpCallContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.Set<McpInvocation>().Add(new McpInvocation
            {
                OccurredAt = clock.UtcNow,
                Method = McpJson.Truncate(record.Method, 60),
                Target = record.Target is null ? null : McpJson.Truncate(record.Target, 250),
                ApiKeyId = context.ApiKeyId,
                ActorName = McpJson.Truncate(context.ActorName, 250),
                ClientName = record.ClientName is null ? null : McpJson.Truncate(record.ClientName, 120),
                Outcome = record.Outcome,
                DurationMs = record.DurationMs,
                ErrorMessage = record.ErrorMessage is null ? null : McpJson.Truncate(record.ErrorMessage, 990),
                IpAddress = context.IpAddress,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to store MCP invocation {Method} {Target}.", record.Method, record.Target);
        }

        if (record.Method is "tools/call" or "initialize")
        {
            await audit.WriteAsync(
                new AuditEntry("mcp", record.Method == "initialize" ? "client.initialize" : "tool.call", record.Outcome switch
                {
                    McpInvocationOutcome.Success => AuditOutcome.Success,
                    McpInvocationOutcome.Denied => AuditOutcome.Denied,
                    _ => AuditOutcome.Failure,
                })
                {
                    TargetType = record.Method == "initialize" ? "mcp_client" : "mcp_tool",
                    TargetName = record.Target,
                    Message = record.ErrorMessage,
                    Details = new { apiKeyId = context.ApiKeyId, outcome = record.Outcome.ToString(), durationMs = record.DurationMs },
                },
                cancellationToken);
        }
    }
}
