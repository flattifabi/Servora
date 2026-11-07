using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Tables;

namespace Servora.Application.Mcp.Tools;

/// <summary>Searches records of a table through <see cref="IRecordService"/> (table permissions enforced there).</summary>
/// <param name="records">The record service.</param>
public sealed class SearchRecordsTool(IRecordService records) : McpToolBase
{
    /// <inheritdoc />
    public override string Name => "search_records";

    /// <inheritdoc />
    public override string Title => "Search records";

    /// <inheritdoc />
    public override string Description =>
        "Free-text search in a Servora table (for example incident, problem, change_request, sc_request, cmdb_ci, kb_knowledge, sys_user). " +
        "Returns at most 50 records with their fields. Use the servora://tables resource to discover table names.";

    /// <inheritdoc />
    public override string? RequiredPermission => null;

    /// <inheritdoc />
    protected override string SchemaJson => """
        {
          "type": "object",
          "properties": {
            "table": { "type": "string", "description": "Technical table name, for example incident.", "pattern": "^[a-z0-9_]{1,64}$" },
            "query": { "type": "string", "description": "Free-text search term (optional).", "maxLength": 200 },
            "fields": { "type": "array", "description": "Fields to return (optional, default all).", "items": { "type": "string", "maxLength": 64 }, "maxItems": 30 },
            "limit": { "type": "integer", "description": "Maximum number of records (1-50, default 10).", "minimum": 1, "maximum": 50 }
          },
          "required": ["table"],
          "additionalProperties": false
        }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var table = McpJson.GetString(arguments, "table")!;
        var limit = Math.Clamp(McpJson.GetInt(arguments, "limit", 10), 1, McpLimits.MaxRecords);
        var query = new RecordQuery
        {
            Page = new PageRequest { PageSize = limit, Search = McpJson.GetString(arguments, "query") },
            Fields = McpJson.GetStringArray(arguments, "fields"),
        };

        var result = await records.QueryAsync(table, query, cancellationToken);
        if (!result.Succeeded)
        {
            return Fail(result);
        }

        var page = result.Value!;
        return Ok(new { table, totalCount = page.TotalCount, returned = page.Items.Count, records = page.Items });
    }
}
