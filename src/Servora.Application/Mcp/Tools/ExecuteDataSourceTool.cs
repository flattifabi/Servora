using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Integration;
using Servora.Application.Security;

namespace Servora.Application.Mcp.Tools;

/// <summary>
/// Executes an <b>approved</b> data source endpoint through <see cref="IDataSourceExecutor"/> (SSRF protection, timeouts and
/// size limits are enforced there). Never accepts a URL.
/// </summary>
/// <param name="executor">The data source executor.</param>
public sealed class ExecuteDataSourceTool(IDataSourceExecutor executor) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "execute_data_source";

    /// <inheritdoc />
    public override string Title => "Execute data source endpoint";

    /// <inheritdoc />
    public override string Description => "Executes an approved endpoint of a configured data source with parameters. Use list_data_sources first.";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Integration.ExecuteDataSources;

    /// <inheritdoc />
    public override bool IsReadOnly => false;

    /// <inheritdoc />
    public override bool IsConsequential => true;

    /// <inheritdoc />
    protected override string SchemaJson => """
        {
          "type": "object",
          "properties": {
            "data_source_id": { "type": "string", "description": "Data source id (UUID).", "format": "uuid" },
            "endpoint": { "type": "string", "description": "Endpoint key.", "pattern": "^[A-Za-z0-9_.-]{1,64}$" },
            "parameters": {
              "type": "object",
              "description": "Parameter values by name.",
              "maxProperties": 30,
              "additionalProperties": { "type": ["string", "number", "boolean"], "maxLength": 2000 }
            }
          },
          "required": ["data_source_id", "endpoint"],
          "additionalProperties": false
        }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            McpJson.GetGuid(arguments, "data_source_id")!.Value,
            McpJson.GetString(arguments, "endpoint")!,
            McpJson.GetStringMap(arguments, "parameters"),
            cancellationToken);
        if (!result.Succeeded)
        {
            return Fail(result);
        }

        var response = result.Value!;
        return Ok(new
        {
            statusCode = response.StatusCode,
            contentType = response.ContentType,
            durationMs = response.DurationMs,
            truncated = response.Truncated,
            body = McpJson.Truncate(response.Body, McpLimits.MaxOutputChars / 2),
        });
    }
}
