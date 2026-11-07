using System.Text.Json;
using System.Text.Json.Nodes;
using Servora.Application.Automation;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Application.Mcp.Tools;

/// <summary>Starts a flow (<see cref="IFlowExecutionService"/>). Consequential: AI agents need user confirmation.</summary>
/// <param name="flows">The flow execution service.</param>
public sealed class StartFlowTool(IFlowExecutionService flows) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "start_flow";

    /// <inheritdoc />
    public override string Title => "Start flow";

    /// <inheritdoc />
    public override string Description => "Starts an automation flow with optional input values and returns the run id. Use list_flows first.";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Automation.Execute;

    /// <inheritdoc />
    public override bool IsReadOnly => false;

    /// <inheritdoc />
    public override bool IsConsequential => true;

    /// <inheritdoc />
    protected override string SchemaJson => """
        {
          "type": "object",
          "properties": {
            "flow_id": { "type": "string", "description": "Flow id (UUID).", "format": "uuid" },
            "inputs": {
              "type": "object",
              "description": "Flow input values by name.",
              "maxProperties": 30,
              "additionalProperties": { "type": ["string", "number", "boolean", "null"], "maxLength": 4000 }
            }
          },
          "required": ["flow_id"],
          "additionalProperties": false
        }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var inputs = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (arguments["inputs"] is JsonObject map)
        {
            foreach (var (key, value) in map)
            {
                inputs[key] = value?.GetValueKind() switch
                {
                    JsonValueKind.String => value.GetValue<string>(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => value.ToJsonString(),
                    _ => null,
                };
            }
        }

        var flowId = McpJson.GetGuid(arguments, "flow_id")!.Value;
        var started = await flows.StartAsync(flowId, inputs, cancellationToken);
        return started.Succeeded ? Ok(new { runId = started.Value, flowId, message = "Flow run queued." }) : Fail(started);
    }
}
