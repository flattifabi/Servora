using System.Text.Json.Nodes;
using Servora.Application.Automation;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Application.Mcp.Tools;

/// <summary>Gets the status of a flow run (<see cref="IFlowExecutionService"/>).</summary>
/// <param name="flows">The flow execution service.</param>
public sealed class GetFlowRunTool(IFlowExecutionService flows) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "get_flow_run";

    /// <inheritdoc />
    public override string Title => "Get flow run";

    /// <inheritdoc />
    public override string Description => "Returns the status (Queued, Running, Waiting, Succeeded, Failed, Cancelled) of a flow run.";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Automation.Read;

    /// <inheritdoc />
    protected override string SchemaJson => """
        {
          "type": "object",
          "properties": { "run_id": { "type": "string", "description": "Run id (UUID).", "format": "uuid" } },
          "required": ["run_id"],
          "additionalProperties": false
        }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var run = await flows.GetRunAsync(McpJson.GetGuid(arguments, "run_id")!.Value, cancellationToken);
        return run.Succeeded ? Ok(run.Value) : Fail(run);
    }
}
