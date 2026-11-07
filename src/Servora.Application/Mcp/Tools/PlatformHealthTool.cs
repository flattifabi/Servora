using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Application.Mcp.Tools;

/// <summary>Reports basic platform health (version, database reachability).</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="clock">The clock.</param>
public sealed class PlatformHealthTool(IAppDbContextFactory dbFactory, IClock clock) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "platform_health";

    /// <inheritdoc />
    public override string Title => "Platform health";

    /// <inheritdoc />
    public override string Description => "Returns the health of the Servora instance: version, database connectivity and server time.";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Observability.ReadHealth;

    /// <inheritdoc />
    protected override string SchemaJson => """
        { "type": "object", "properties": {}, "additionalProperties": false }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        bool databaseReachable;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            databaseReachable = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            databaseReachable = false;
        }

        return Ok(new
        {
            status = databaseReachable ? "healthy" : "degraded",
            version = McpProtocol.ServerVersion,
            database = databaseReachable ? "reachable" : "unreachable",
            serverTimeUtc = clock.UtcNow,
            machine = Environment.MachineName,
        });
    }
}
