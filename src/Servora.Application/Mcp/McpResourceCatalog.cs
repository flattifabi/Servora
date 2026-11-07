using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Application.Tables;

namespace Servora.Application.Mcp;

/// <summary>
/// Read-only MCP resources: server information, the table catalog and table schemas. Table metadata requires
/// <c>platform.records.read</c>; record data is only available through permission-checked tools.
/// </summary>
/// <param name="tables">The table registry.</param>
/// <param name="currentUser">The current actor.</param>
public sealed partial class McpResourceCatalog(ITableRegistry tables, ICurrentUser currentUser)
{
    /// <summary>The URI of the server information resource.</summary>
    public const string ServerInfoUri = "servora://server/info";

    /// <summary>The URI of the table catalog resource.</summary>
    public const string TablesUri = "servora://tables";

    /// <summary>The URI template of table schema resources.</summary>
    public const string TableTemplate = "servora://tables/{name}";

    /// <summary>Builds the <c>resources/list</c> result.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result node.</returns>
    public async Task<JsonObject> ListAsync(CancellationToken cancellationToken)
    {
        var resources = new JsonArray
        {
            Resource(ServerInfoUri, "server-info", "Servora server information", "Server version, supported MCP revisions and the calling identity."),
        };

        if (await currentUser.HasPermissionAsync(Permissions.Platform.ReadRecords, cancellationToken))
        {
            resources.Add(Resource(TablesUri, "tables", "Table catalog", "All platform tables with labels and display fields."));
        }

        return new JsonObject { ["resources"] = resources };
    }

    /// <summary>Builds the <c>resources/templates/list</c> result.</summary>
    /// <returns>The result node.</returns>
    public static JsonObject ListTemplates() => new()
    {
        ["resourceTemplates"] = new JsonArray
        {
            new JsonObject
            {
                ["uriTemplate"] = TableTemplate,
                ["name"] = "table-schema",
                ["title"] = "Table schema",
                ["description"] = "Fields, types, references and choices of one table (requires platform.records.read).",
                ["mimeType"] = "application/json",
            },
        },
    };

    /// <summary>Reads a resource.</summary>
    /// <param name="uri">The resource URI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <c>resources/read</c> outcome.</returns>
    public async Task<McpRpcOutcome> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        if (uri == ServerInfoUri)
        {
            var info = new JsonObject
            {
                ["name"] = McpProtocol.ServerName,
                ["version"] = McpProtocol.ServerVersion,
                ["protocolVersions"] = new JsonArray(McpProtocol.SupportedVersions.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()),
                ["caller"] = new JsonObject { ["name"] = currentUser.DisplayName, ["actorType"] = currentUser.ActorType.ToString() },
            };
            return Contents(uri, info);
        }

        var isCatalog = uri == TablesUri;
        var match = TableUri().Match(uri);
        if (!isCatalog && !match.Success)
        {
            return McpRpcOutcome.Error(McpErrorCodes.ResourceNotFound, "Resource not found.", new JsonObject { ["uri"] = McpJson.Truncate(uri, 200) });
        }

        if (!await currentUser.HasPermissionAsync(Permissions.Platform.ReadRecords, cancellationToken))
        {
            return McpRpcOutcome.Error(McpErrorCodes.Forbidden, $"Permission '{Permissions.Platform.ReadRecords}' is required to read table metadata.");
        }

        if (isCatalog)
        {
            var all = await tables.GetTablesAsync(cancellationToken);
            var list = new JsonArray(all.Take(500).Select(t => (JsonNode)new JsonObject
            {
                ["name"] = t.Name,
                ["label"] = t.Label,
                ["pluralLabel"] = t.PluralLabel,
                ["displayField"] = t.DisplayField,
                ["isCustom"] = t.IsCustom,
            }).ToArray());
            return Contents(uri, new JsonObject { ["tables"] = list });
        }

        var table = await tables.GetTableAsync(match.Groups["name"].Value, cancellationToken);
        if (table is null)
        {
            return McpRpcOutcome.Error(McpErrorCodes.ResourceNotFound, "Resource not found.", new JsonObject { ["uri"] = uri });
        }

        var fields = new JsonArray(table.Fields.Take(500).Select(f => (JsonNode)McpJson.ToNode(new
        {
            f.Name,
            f.Label,
            Type = f.Type.ToString(),
            f.ReferenceTable,
            f.IsRequired,
            f.IsReadOnly,
            f.MaxLength,
            Choices = f.Choices.Count == 0 ? null : f.Choices,
        })).ToArray());
        return Contents(uri, new JsonObject { ["name"] = table.Name, ["label"] = table.Label, ["displayField"] = table.DisplayField, ["fields"] = fields });
    }

    private static JsonObject Resource(string uri, string name, string title, string description) => new()
    {
        ["uri"] = uri,
        ["name"] = name,
        ["title"] = title,
        ["description"] = description,
        ["mimeType"] = "application/json",
    };

    private static McpRpcOutcome Contents(string uri, JsonNode body)
    {
        var text = McpJson.ToBoundedText(body, McpLimits.MaxOutputChars, out _);
        return McpRpcOutcome.Ok(new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["uri"] = uri, ["mimeType"] = "application/json", ["text"] = text } },
        });
    }

    [GeneratedRegex("^servora://tables/(?<name>[a-z0-9_]{1,64})$", RegexOptions.CultureInvariant)]
    private static partial Regex TableUri();
}
