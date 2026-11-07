using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Integration;
using Servora.Application.Security;

namespace Servora.Application.Mcp.Tools;

/// <summary>Lists the approved data source endpoints the caller may execute (<see cref="IDataSourceExecutor"/>).</summary>
/// <param name="executor">The data source executor.</param>
public sealed class ListDataSourcesTool(IDataSourceExecutor executor) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "list_data_sources";

    /// <inheritdoc />
    public override string Title => "List data sources";

    /// <inheritdoc />
    public override string Description => "Lists configured REST data sources and their approved endpoints (key, method, parameters).";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Integration.ReadDataSources;

    /// <inheritdoc />
    protected override string SchemaJson => """
        { "type": "object", "properties": {}, "additionalProperties": false }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var sources = await executor.ListAccessibleAsync(cancellationToken);
        return Ok(new { dataSources = sources.Take(100) });
    }
}
