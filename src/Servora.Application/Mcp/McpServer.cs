using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Mcp;

namespace Servora.Application.Mcp;

/// <summary>
/// JSON-RPC 2.0 dispatcher implementing the MCP server methods <c>initialize</c>, <c>ping</c>, <c>tools/list</c>,
/// <c>tools/call</c>, <c>resources/list</c>, <c>resources/templates/list</c> and <c>resources/read</c>. Every tool call
/// requires the key scope <c>mcp.tool.&lt;name&gt;</c> <b>and</b> the tool's underlying permission; arguments are
/// validated against the tool's JSON schema; results are bounded; each call is logged and audited.
/// </summary>
/// <param name="tools">The registered tools.</param>
/// <param name="currentUser">The current actor (API key principal).</param>
/// <param name="state">The enable switches.</param>
/// <param name="resources">The resource catalog.</param>
/// <param name="invocations">The invocation log.</param>
/// <param name="logger">The logger.</param>
public sealed class McpServer(
    IEnumerable<IMcpTool> tools,
    ICurrentUser currentUser,
    IMcpRuntimeStateProvider state,
    McpResourceCatalog resources,
    IMcpInvocationLog invocations,
    ILogger<McpServer> logger) : IMcpServer
{
    private const string Instructions =
        "Servora is an IT service management platform. Use search_records/get_record to look up records (incidents, problems, " +
        "changes, users, configuration items), search_knowledge for knowledge articles and create_incident to report issues. " +
        "All calls run with the permissions of the API key and its owner.";

    private readonly IReadOnlyList<IMcpTool> _tools = tools.ToList();

    /// <inheritdoc />
    public async Task<string?> HandleAsync(string body, McpCallContext context, CancellationToken cancellationToken = default)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body, documentOptions: McpJson.DocumentOptions);
        }
        catch (JsonException)
        {
            return ErrorResponse(null, McpErrorCodes.ParseError, "Parse error: the request body is not valid JSON.").ToJsonString();
        }

        if (root is JsonArray batch)
        {
            if (batch.Count == 0 || batch.Count > McpLimits.MaxBatchSize)
            {
                return ErrorResponse(null, McpErrorCodes.InvalidRequest, $"A batch must contain between 1 and {McpLimits.MaxBatchSize} messages.").ToJsonString();
            }

            var responses = new JsonArray();
            foreach (var message in batch)
            {
                if (await HandleMessageAsync(message, context, cancellationToken) is { } response)
                {
                    responses.Add(response);
                }
            }

            return responses.Count == 0 ? null : responses.ToJsonString();
        }

        return (await HandleMessageAsync(root, context, cancellationToken))?.ToJsonString();
    }

    private static JsonObject ErrorResponse(JsonNode? id, int code, string message, JsonNode? data = null)
    {
        var error = new JsonObject { ["code"] = code, ["message"] = message };
        if (data is not null)
        {
            error["data"] = data;
        }

        return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = error };
    }

    private static JsonObject ToolError(string message) => new()
    {
        ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = message } },
        ["isError"] = true,
    };

    private async Task<JsonObject?> HandleMessageAsync(JsonNode? node, McpCallContext context, CancellationToken cancellationToken)
    {
        if (node is not JsonObject message)
        {
            return ErrorResponse(null, McpErrorCodes.InvalidRequest, "Invalid request: expected a JSON-RPC object.");
        }

        var hasId = message.TryGetPropertyValue("id", out var idNode);
        var id = idNode?.DeepClone();
        if (hasId && idNode?.GetValueKind() is not (JsonValueKind.String or JsonValueKind.Number))
        {
            return ErrorResponse(null, McpErrorCodes.InvalidRequest, "Invalid request: id must be a string or a number.");
        }

        if (message["jsonrpc"]?.GetValueKind() != JsonValueKind.String || message["jsonrpc"]!.GetValue<string>() != "2.0")
        {
            return ErrorResponse(id, McpErrorCodes.InvalidRequest, "Invalid request: jsonrpc must be \"2.0\".");
        }

        if (message["method"] is not JsonValue methodNode || methodNode.GetValueKind() != JsonValueKind.String)
        {
            // Responses to server-initiated requests (the server sends none) are accepted and ignored.
            return message.ContainsKey("result") || message.ContainsKey("error")
                ? null
                : ErrorResponse(id, McpErrorCodes.InvalidRequest, "Invalid request: method is missing.");
        }

        var method = methodNode.GetValue<string>();
        var parameters = message["params"] as JsonObject;
        if (message["params"] is not null && parameters is null)
        {
            return hasId ? ErrorResponse(id, McpErrorCodes.InvalidParams, "Invalid params: params must be an object.") : null;
        }

        McpRpcOutcome outcome;
        try
        {
            outcome = await DispatchAsync(method, parameters ?? new JsonObject(), context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP method {Method} failed.", method);
            outcome = McpRpcOutcome.Error(McpErrorCodes.InternalError, "Internal error.");
        }

        if (!hasId)
        {
            return null;
        }

        if (outcome.IsError)
        {
            return ErrorResponse(id, outcome.ErrorCode!.Value, outcome.ErrorMessage!, outcome.ErrorData);
        }

        return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = outcome.Result ?? new JsonObject() };
    }

    private async Task<McpRpcOutcome> DispatchAsync(string method, JsonObject parameters, McpCallContext context, CancellationToken cancellationToken)
    {
        if (method == "ping")
        {
            return McpRpcOutcome.Ok(new JsonObject());
        }

        if (method.StartsWith("notifications/", StringComparison.Ordinal))
        {
            return McpRpcOutcome.Ok(new JsonObject());
        }

        var runtime = await state.GetAsync(cancellationToken);
        if (!runtime.ServerEnabled)
        {
            return McpRpcOutcome.Error(McpErrorCodes.ServerDisabled, "The MCP server is disabled by the administrator.");
        }

        return method switch
        {
            "initialize" => await InitializeAsync(parameters, context, cancellationToken),
            "tools/list" => McpRpcOutcome.Ok(await ListToolsAsync(runtime, context, cancellationToken)),
            "tools/call" => await CallToolAsync(parameters, runtime, context, cancellationToken),
            "resources/list" => McpRpcOutcome.Ok(await resources.ListAsync(cancellationToken)),
            "resources/templates/list" => McpRpcOutcome.Ok(McpResourceCatalog.ListTemplates()),
            "resources/read" => await ReadResourceAsync(parameters, context, cancellationToken),
            _ => McpRpcOutcome.Error(McpErrorCodes.MethodNotFound, $"Method not found: {McpJson.Truncate(method, 80)}"),
        };
    }

    private async Task<McpRpcOutcome> InitializeAsync(JsonObject parameters, McpCallContext context, CancellationToken cancellationToken)
    {
        var requested = parameters["protocolVersion"] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
        var clientName = (parameters["clientInfo"] as JsonObject)?["name"] is JsonValue n && n.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : null;
        var negotiated = McpProtocol.Negotiate(requested);

        await invocations.RecordAsync(
            new McpInvocationRecord("initialize", clientName ?? "unknown client", McpInvocationOutcome.Success, 0) { ClientName = clientName },
            context,
            cancellationToken);

        return McpRpcOutcome.Ok(new JsonObject
        {
            ["protocolVersion"] = negotiated,
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject { ["listChanged"] = false },
                ["resources"] = new JsonObject { ["subscribe"] = false, ["listChanged"] = false },
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = McpProtocol.ServerName,
                ["title"] = "Servora",
                ["version"] = McpProtocol.ServerVersion,
            },
            ["instructions"] = Instructions,
        });
    }

    private async Task<JsonObject> ListToolsAsync(McpRuntimeState runtime, McpCallContext context, CancellationToken cancellationToken)
    {
        var list = new JsonArray();
        foreach (var tool in _tools.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (!runtime.IsToolEnabled(tool.Name) || !PermissionMatcher.IsGranted(context.Scopes, McpScopes.ForTool(tool.Name)))
            {
                continue;
            }

            if (tool.RequiredPermission is { } permission && !await currentUser.HasPermissionAsync(permission, cancellationToken))
            {
                continue;
            }

            list.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["title"] = tool.Title,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.CreateInputSchema(),
                ["annotations"] = new JsonObject
                {
                    ["title"] = tool.Title,
                    ["readOnlyHint"] = tool.IsReadOnly,
                    ["destructiveHint"] = tool.IsConsequential,
                    ["openWorldHint"] = false,
                },
            });
        }

        return new JsonObject { ["tools"] = list };
    }

    private async Task<McpRpcOutcome> CallToolAsync(JsonObject parameters, McpRuntimeState runtime, McpCallContext context, CancellationToken cancellationToken)
    {
        var name = parameters["name"] is JsonValue n && n.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : null;
        if (string.IsNullOrEmpty(name))
        {
            return McpRpcOutcome.Error(McpErrorCodes.InvalidParams, "Invalid params: the tool name is missing.");
        }

        var tool = _tools.FirstOrDefault(t => t.Name == name);
        if (tool is null || !runtime.IsToolEnabled(name))
        {
            return McpRpcOutcome.Error(McpErrorCodes.InvalidParams, $"Unknown tool: {McpJson.Truncate(name, 64)}");
        }

        var stopwatch = Stopwatch.StartNew();
        var scope = McpScopes.ForTool(tool.Name);
        if (!PermissionMatcher.IsGranted(context.Scopes, scope))
        {
            await LogAsync(tool.Name, McpInvocationOutcome.Denied, stopwatch, $"Missing scope '{scope}'.", context, cancellationToken);
            return McpRpcOutcome.Error(McpErrorCodes.Forbidden, $"Forbidden: the API key lacks the scope '{scope}'.");
        }

        if (tool.RequiredPermission is { } permission && !await currentUser.HasPermissionAsync(permission, cancellationToken))
        {
            await LogAsync(tool.Name, McpInvocationOutcome.Denied, stopwatch, $"Missing permission '{permission}'.", context, cancellationToken);
            return McpRpcOutcome.Error(McpErrorCodes.Forbidden, $"Forbidden: the permission '{permission}' is required (as key scope and for the key owner).");
        }

        var argumentsNode = parameters["arguments"];
        if (argumentsNode is not null && argumentsNode is not JsonObject)
        {
            await LogAsync(tool.Name, McpInvocationOutcome.InvalidParams, stopwatch, "Arguments must be an object.", context, cancellationToken);
            return McpRpcOutcome.Error(McpErrorCodes.InvalidParams, "Invalid params: arguments must be an object.");
        }

        var arguments = (JsonObject?)argumentsNode?.DeepClone() ?? new JsonObject();
        var errors = JsonSchemaValidator.Validate(arguments, tool.CreateInputSchema());
        if (errors.Count > 0)
        {
            await LogAsync(tool.Name, McpInvocationOutcome.InvalidParams, stopwatch, string.Join(" ", errors), context, cancellationToken);
            return McpRpcOutcome.Error(
                McpErrorCodes.InvalidParams,
                "Invalid params: the arguments do not match the tool's input schema.",
                new JsonObject { ["errors"] = new JsonArray(errors.Select(e => (JsonNode)JsonValue.Create(e)!).ToArray()) });
        }

        Result<JsonNode> result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(McpLimits.ToolTimeout);
            try
            {
                result = await tool.ExecuteAsync(arguments, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = Result<JsonNode>.Failure($"The tool did not complete within {McpLimits.ToolTimeout.TotalSeconds:0} seconds.");
            }
        }

        if (!result.Succeeded)
        {
            var outcome = result.ErrorKind == ResultErrorKind.Forbidden ? McpInvocationOutcome.Denied : McpInvocationOutcome.ToolError;
            await LogAsync(tool.Name, outcome, stopwatch, result.Error, context, cancellationToken);
            return McpRpcOutcome.Ok(ToolError(result.Error ?? "The tool failed."));
        }

        var text = McpJson.ToBoundedText(result.Value, McpLimits.MaxOutputChars, out var truncated);
        var response = new JsonObject
        {
            ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } },
            ["isError"] = false,
        };
        if (!truncated && result.Value is JsonObject structured)
        {
            response["structuredContent"] = structured.DeepClone();
        }

        await LogAsync(tool.Name, McpInvocationOutcome.Success, stopwatch, truncated ? "Result truncated at the output limit." : null, context, cancellationToken);
        return McpRpcOutcome.Ok(response);
    }

    private async Task<McpRpcOutcome> ReadResourceAsync(JsonObject parameters, McpCallContext context, CancellationToken cancellationToken)
    {
        var uri = parameters["uri"] is JsonValue u && u.GetValueKind() == JsonValueKind.String ? u.GetValue<string>() : null;
        if (string.IsNullOrEmpty(uri) || uri.Length > 300)
        {
            return McpRpcOutcome.Error(McpErrorCodes.InvalidParams, "Invalid params: uri is missing or too long.");
        }

        var stopwatch = Stopwatch.StartNew();
        var outcome = await resources.ReadAsync(uri, cancellationToken);
        await invocations.RecordAsync(
            new McpInvocationRecord(
                "resources/read",
                uri,
                outcome.IsError ? (outcome.ErrorCode == McpErrorCodes.Forbidden ? McpInvocationOutcome.Denied : McpInvocationOutcome.ToolError) : McpInvocationOutcome.Success,
                stopwatch.ElapsedMilliseconds,
                outcome.ErrorMessage),
            context,
            cancellationToken);
        return outcome;
    }

    private Task LogAsync(string toolName, McpInvocationOutcome outcome, Stopwatch stopwatch, string? error, McpCallContext context, CancellationToken cancellationToken) =>
        invocations.RecordAsync(new McpInvocationRecord("tools/call", toolName, outcome, stopwatch.ElapsedMilliseconds, error), context, cancellationToken);
}
