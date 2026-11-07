using Servora.Application.Mcp;

namespace Servora.Tests.Unit.Mcp.Fakes;

/// <summary><see cref="IMcpInvocationLog"/> that keeps the records in memory.</summary>
internal sealed class RecordingMcpInvocationLog : IMcpInvocationLog
{
    /// <summary>Gets the recorded invocations.</summary>
    public List<McpInvocationRecord> Records { get; } = [];

    /// <inheritdoc />
    public Task RecordAsync(McpInvocationRecord record, McpCallContext context, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }
}
