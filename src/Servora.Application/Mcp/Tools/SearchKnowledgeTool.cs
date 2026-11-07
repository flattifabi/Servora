using System.Text.Json.Nodes;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Application.Tables;
using Servora.Domain.Common;

namespace Servora.Application.Mcp.Tools;

/// <summary>Searches knowledge articles (<c>kb_knowledge</c>) through <see cref="IRecordService"/>.</summary>
/// <param name="records">The record service.</param>
public sealed class SearchKnowledgeTool(IRecordService records) : McpToolBase
{
    /// <summary>The maximum number of characters returned per text field.</summary>
    private const int MaxFieldChars = 4000;

    /// <inheritdoc />
    public override string Name => "search_knowledge";

    /// <inheritdoc />
    public override string Title => "Search knowledge";

    /// <inheritdoc />
    public override string Description => "Searches knowledge base articles and returns the best matches (at most 10) including their text.";

    /// <inheritdoc />
    public override string? RequiredPermission => Permissions.Knowledge.Read;

    /// <inheritdoc />
    protected override string SchemaJson => """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "What to search for.", "minLength": 2, "maxLength": 200 },
            "limit": { "type": "integer", "description": "Maximum number of articles (1-10, default 5).", "minimum": 1, "maximum": 10 }
          },
          "required": ["query"],
          "additionalProperties": false
        }
        """;

    /// <inheritdoc />
    public override async Task<Result<JsonNode>> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        var query = new RecordQuery
        {
            Page = new PageRequest { PageSize = Math.Clamp(McpJson.GetInt(arguments, "limit", 5), 1, 10), Search = McpJson.GetString(arguments, "query") },
        };

        var result = await records.QueryAsync(TableNames.KnowledgeArticle, query, cancellationToken);
        if (!result.Succeeded)
        {
            return Fail(result);
        }

        var articles = result.Value!.Items
            .Select(item => item.ToDictionary(p => p.Key, p => p.Value is string s ? McpJson.Truncate(s, MaxFieldChars) : p.Value))
            .ToList();
        return Ok(new { totalCount = result.Value.TotalCount, articles });
    }
}
