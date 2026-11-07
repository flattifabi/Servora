using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Security;
using Servora.Domain.Mcp;
using Servora.Infrastructure.Platform;
using Servora.Tests.Unit.Api.Fakes;
using Servora.Tests.Unit.Mcp.Fakes;

namespace Servora.Tests.Unit.Mcp;

/// <summary>Tests of the JSON-RPC 2.0 dispatch of <see cref="McpServer"/>: protocol, scopes, permissions and input validation.</summary>
public sealed class McpServerTests
{
    private readonly TestCurrentUser _user = new();
    private readonly StaticMcpRuntimeStateProvider _state = new();
    private readonly RecordingMcpInvocationLog _log = new();
    private readonly FakeMcpTool _search = new("search_things", Permissions.Incidents.Read);
    private readonly FakeMcpTool _create = new("create_thing", Permissions.Incidents.Create, consequential: true);

    [Fact]
    public async Task Initialize_NegotiatesTheRequestedVersionWhenSupported()
    {
        var response = await SendAsync(Request(1, "initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "claude-desktop", ["version"] = "1.0" },
        }));

        var result = response["result"]!;
        Assert.Equal("2.0", response["jsonrpc"]!.GetValue<string>());
        Assert.Equal(1, response["id"]!.GetValue<int>());
        Assert.Equal("2025-06-18", result["protocolVersion"]!.GetValue<string>());
        Assert.Equal(McpProtocol.ServerName, result["serverInfo"]!["name"]!.GetValue<string>());
        Assert.NotNull(result["capabilities"]!["tools"]);
        Assert.Contains(_log.Records, r => r.Method == "initialize" && r.ClientName == "claude-desktop");
    }

    [Fact]
    public async Task Initialize_FallsBackToTheLatestVersionForUnknownRevisions()
    {
        var response = await SendAsync(Request("a", "initialize", new JsonObject { ["protocolVersion"] = "1999-01-01" }));

        Assert.Equal(McpProtocol.LatestVersion, response["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.Equal("a", response["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task ToolsList_OnlyShowsToolsCoveredByScopeAndPermission()
    {
        _user.Grant(Permissions.Incidents.Read);

        var all = await SendAsync(Request(1, "tools/list"), "mcp.tool.*");
        var names = all["result"]!["tools"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();
        Assert.Equal(["search_things"], names);

        var tool = all["result"]!["tools"]![0]!;
        Assert.Equal("object", tool["inputSchema"]!["type"]!.GetValue<string>());
        Assert.True(tool["annotations"]!["readOnlyHint"]!.GetValue<bool>());

        var none = await SendAsync(Request(2, "tools/list"), "mcp.access");
        Assert.Empty(none["result"]!["tools"]!.AsArray());
    }

    [Fact]
    public async Task ToolsList_HidesToolsDisabledByTheAdministrator()
    {
        _state.DisabledTools.Add("search_things");

        var response = await SendAsync(Request(1, "tools/list"), "mcp.tool.*");

        Assert.DoesNotContain(response["result"]!["tools"]!.AsArray(), t => t!["name"]!.GetValue<string>() == "search_things");
    }

    [Fact]
    public async Task ToolsCall_WithScopeAndPermission_ExecutesTheTool()
    {
        var response = await SendAsync(CallTool(7, "search_things", new JsonObject { ["query"] = "printer" }), "mcp.tool.search_things");

        var result = response["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());
        Assert.Contains("printer", result["content"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("printer", result["structuredContent"]!["echo"]!.GetValue<string>());
        Assert.Single(_search.Calls);
        Assert.Contains(_log.Records, r => r.Method == "tools/call" && r.Target == "search_things" && r.Outcome == McpInvocationOutcome.Success);
    }

    [Fact]
    public async Task ToolsCall_WithoutToolScope_IsDeniedAndLogged()
    {
        var response = await SendAsync(CallTool(1, "search_things", new JsonObject { ["query"] = "x" }), "mcp.tool.other_tool");

        Assert.Equal(McpErrorCodes.Forbidden, response["error"]!["code"]!.GetValue<int>());
        Assert.Contains("mcp.tool.search_things", response["error"]!["message"]!.GetValue<string>());
        Assert.Empty(_search.Calls);
        Assert.Contains(_log.Records, r => r.Outcome == McpInvocationOutcome.Denied);
    }

    [Fact]
    public async Task ToolsCall_WithScopeButWithoutUnderlyingPermission_IsDenied()
    {
        _user.Grant(Permissions.Incidents.Read);

        var response = await SendAsync(CallTool(1, "create_thing", new JsonObject { ["query"] = "x" }), "mcp.tool.*");

        Assert.Equal(McpErrorCodes.Forbidden, response["error"]!["code"]!.GetValue<int>());
        Assert.Contains(Permissions.Incidents.Create, response["error"]!["message"]!.GetValue<string>());
        Assert.Empty(_create.Calls);
    }

    [Fact]
    public async Task ToolsCall_InvalidArguments_AreRejectedBeforeExecution()
    {
        var response = await SendAsync(CallTool(1, "search_things", new JsonObject { ["limit"] = 500, ["unexpected"] = true }), "mcp.tool.*");

        var error = response["error"]!;
        Assert.Equal(McpErrorCodes.InvalidParams, error["code"]!.GetValue<int>());
        var errors = error["data"]!["errors"]!.AsArray().Select(e => e!.GetValue<string>()).ToList();
        Assert.Contains(errors, e => e.Contains("query"));
        Assert.Contains(errors, e => e.Contains("limit"));
        Assert.Contains(errors, e => e.Contains("unexpected"));
        Assert.Empty(_search.Calls);
        Assert.Contains(_log.Records, r => r.Outcome == McpInvocationOutcome.InvalidParams);
    }

    [Fact]
    public async Task ToolsCall_ArgumentsMustBeAnObject()
    {
        var request = Request(1, "tools/call", new JsonObject { ["name"] = "search_things", ["arguments"] = new JsonArray(1, 2) });

        var response = await SendAsync(request, "mcp.tool.*");

        Assert.Equal(McpErrorCodes.InvalidParams, response["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task ToolsCall_UnknownTool_IsInvalidParams()
    {
        var response = await SendAsync(CallTool(1, "drop_database", []), "mcp.tool.*");

        Assert.Equal(McpErrorCodes.InvalidParams, response["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task ToolsCall_ToolFailure_IsReturnedAsToolErrorResult()
    {
        _search.Handler = _ => Result<JsonNode>.Failure("Backend unavailable.");

        var response = await SendAsync(CallTool(1, "search_things", new JsonObject { ["query"] = "x" }), "mcp.tool.*");

        Assert.True(response["result"]!["isError"]!.GetValue<bool>());
        Assert.Equal("Backend unavailable.", response["result"]!["content"]![0]!["text"]!.GetValue<string>());
        Assert.Contains(_log.Records, r => r.Outcome == McpInvocationOutcome.ToolError);
    }

    [Fact]
    public async Task ToolsCall_LargeResults_AreTruncated()
    {
        _search.Handler = _ => Result<JsonNode>.Success(new JsonObject { ["blob"] = new string('x', McpLimits.MaxOutputChars * 2) });

        var response = await SendAsync(CallTool(1, "search_things", new JsonObject { ["query"] = "x" }), "mcp.tool.*");

        var text = response["result"]!["content"]![0]!["text"]!.GetValue<string>();
        Assert.True(text.Length <= McpLimits.MaxOutputChars + 200);
        Assert.Null(response["result"]!["structuredContent"]);
    }

    [Fact]
    public async Task ParseError_ForInvalidJson()
    {
        var raw = await CreateServer().HandleAsync("{ not json", Context());

        var response = JsonNode.Parse(raw!)!;
        Assert.Equal(McpErrorCodes.ParseError, response["error"]!["code"]!.GetValue<int>());
        Assert.Null(response["id"]);
    }

    [Theory]
    [InlineData("""{"jsonrpc":"1.0","id":1,"method":"ping"}""")]
    [InlineData("""{"id":1,"method":"ping"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":{"x":1},"method":"ping"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1}""")]
    [InlineData("42")]
    public async Task InvalidRequests_AreRejected(string body)
    {
        var raw = await CreateServer().HandleAsync(body, Context());

        Assert.Equal(McpErrorCodes.InvalidRequest, JsonNode.Parse(raw!)!["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task UnknownMethod_IsMethodNotFound()
    {
        var response = await SendAsync(Request(1, "sampling/createMessage"));

        Assert.Equal(McpErrorCodes.MethodNotFound, response["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task Notifications_ProduceNoResponse()
    {
        var raw = await CreateServer().HandleAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", Context());

        Assert.Null(raw);
    }

    [Fact]
    public async Task Ping_ReturnsEmptyResult()
    {
        var response = await SendAsync(Request(3, "ping"));

        Assert.Empty(response["result"]!.AsObject());
    }

    [Fact]
    public async Task Batch_ReturnsOneResponsePerRequestAndSkipsNotifications()
    {
        var batch = new JsonArray
        {
            Request(1, "ping"),
            JsonNode.Parse("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""),
            Request(2, "tools/list"),
        };

        var raw = await CreateServer().HandleAsync(batch.ToJsonString(), Context("mcp.tool.*"));

        var responses = JsonNode.Parse(raw!)!.AsArray();
        Assert.Equal(2, responses.Count);
        Assert.Equal([1, 2], responses.Select(r => r!["id"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Batch_RejectsEmptyAndOversizedBatches()
    {
        var empty = JsonNode.Parse((await CreateServer().HandleAsync("[]", Context()))!)!;
        Assert.Equal(McpErrorCodes.InvalidRequest, empty["error"]!["code"]!.GetValue<int>());

        var big = new JsonArray(Enumerable.Range(0, McpLimits.MaxBatchSize + 1).Select(i => (JsonNode?)Request(i, "ping")).ToArray());
        var oversized = JsonNode.Parse((await CreateServer().HandleAsync(big.ToJsonString(), Context()))!)!;
        Assert.Equal(McpErrorCodes.InvalidRequest, oversized["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task DisabledServer_RejectsEverythingButPing()
    {
        _state.ServerEnabled = false;

        var list = await SendAsync(Request(1, "tools/list"), "mcp.tool.*");
        var ping = await SendAsync(Request(2, "ping"));

        Assert.Equal(McpErrorCodes.ServerDisabled, list["error"]!["code"]!.GetValue<int>());
        Assert.NotNull(ping["result"]);
    }

    [Fact]
    public async Task ResourcesList_AndServerInfoRead()
    {
        var list = await SendAsync(Request(1, "resources/list"));
        Assert.Contains(list["result"]!["resources"]!.AsArray(), r => r!["uri"]!.GetValue<string>() == McpResourceCatalog.ServerInfoUri);

        var read = await SendAsync(Request(2, "resources/read", new JsonObject { ["uri"] = McpResourceCatalog.ServerInfoUri }));
        Assert.NotNull(read["result"]!["contents"]);

        var missing = await SendAsync(Request(3, "resources/read", new JsonObject { ["uri"] = "servora://nothing" }));
        Assert.Equal(McpErrorCodes.ResourceNotFound, missing["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task ResourcesRead_TableMetadataRequiresPermission()
    {
        _user.Grant(Permissions.Mcp.Access);

        var response = await SendAsync(Request(1, "resources/read", new JsonObject { ["uri"] = McpResourceCatalog.TablesUri }));

        Assert.Equal(McpErrorCodes.Forbidden, response["error"]!["code"]!.GetValue<int>());
    }

    private static JsonObject Request(JsonNode id, string method, JsonObject? parameters = null)
    {
        var request = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        return request;
    }

    private static JsonObject CallTool(int id, string name, JsonObject arguments) =>
        Request(id, "tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments });

    private static McpCallContext Context(params string[] scopes) =>
        new(Guid.NewGuid(), scopes.ToHashSet(StringComparer.Ordinal), "Test key", "127.0.0.1");

    private McpServer CreateServer() =>
        new([_search, _create], _user, _state, new McpResourceCatalog(new ReflectionTableRegistry(), _user), _log, NullLogger<McpServer>.Instance);

    private async Task<JsonNode> SendAsync(JsonObject request, params string[] scopes)
    {
        var raw = await CreateServer().HandleAsync(request.ToJsonString(), Context(scopes));
        Assert.NotNull(raw);
        return JsonNode.Parse(raw)!;
    }
}
