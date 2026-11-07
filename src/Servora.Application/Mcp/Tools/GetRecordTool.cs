using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Tables;

namespace Servora.Application.Mcp.Tools;

/// <summary>Reads one record through <see cref="IRecordService"/> (table permissions enforced there).</summary>
/// <param name="records">The record service.</param>
public sealed class GetRecordTool(IRecordService records) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "get_record";

    /// <inheritdoc />
    public override string Title => "Get record";

    /// <inheritdoc />
    public override string Description => "Reads a single record of a Servora table by its id (UUID). Sensitive fields are never returned.";

    /// <inheritdoc />
    public override string? RequiredPermission => null;

    /// <inheritdoc />
    protected override string SchemaJson => """
        {
          "type": "object",
          "properties": {
            "table": { "type": "string", "description": "Technical table name, for example incident.", "pattern": "^[a-z0-9_]{1,64}$" },
            "id": { "type": "string", "description": "Record id (UUID).", "format": "uuid" }
          },
          "required": ["table", "id"],
          "additionalProperties": false
        }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var table = McpJson.GetString(arguments, "table")!;
        var id = McpJson.GetGuid(arguments, "id")!.Value;
        var result = await records.GetAsync(table, id, cancellationToken);
        return result.Succeeded ? Ok(new { table, record = result.Value }) : Fail(result);
    }
}
