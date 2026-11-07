using System.Text.Json.Nodes;
using Servora.Application.Automation;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Application.Mcp.Tools;

/// <summary>Lists flows the caller may start (<see cref="IFlowExecutionService"/>).</summary>
/// <param name="flows">The flow execution service.</param>
public sealed class ListFlowsTool(IFlowExecutionService flows) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "list_flows";

    /// <inheritdoc />
    public override string Title => "List flows";

    /// <inheritdoc />
    public override string Description => "Lists automation flows that can be started manually.";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Automation.Read;

    /// <inheritdoc />
    protected override string SchemaJson => """
        { "type": "object", "properties": {}, "additionalProperties": false }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var list = await flows.ListStartableAsync(cancellationToken);
        return Ok(new { flows = list.Take(200) });
    }
}
