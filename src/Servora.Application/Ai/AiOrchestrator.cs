using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Security;
using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>Default <see cref="IAiOrchestrator"/>.</summary>
/// <param name="chat">The chat client.</param>
/// <param name="agents">The registered agents.</param>
/// <param name="scopeFactory">The scope factory (one scope per agent run, with the agent identity).</param>
/// <param name="permissions">The permission service (user permissions for the agent intersection).</param>
/// <param name="logger">The logger.</param>
public sealed class AiOrchestrator(
    IAiChatClient chat,
    IEnumerable<IAiAgent> agents,
    IServiceScopeFactory scopeFactory,
    IPermissionService permissions,
    ILogger<AiOrchestrator> logger) : IAiOrchestrator
{
    /// <summary>The agent key used for orchestrator steps.</summary>
    public const string OrchestratorKey = "orchestrator";

    /// <summary>The prefix of the delegation tools offered to the orchestrator model.</summary>
    public const string DelegatePrefix = "delegate_to_";

    private const int MaxOrchestratorRounds = 3;
    private const int MaxAgentRounds = 4;
    private const int MaxToolResultChars = 8000;

    private const string PendingActionNote =
        "This consequential action was NOT executed. It has been proposed to the user as a confirmation card; the user must confirm it in the UI.";

    private readonly IReadOnlyList<IAiAgent> _agents = agents.ToList();

    /// <inheritdoc />
    public IReadOnlyList<IAiAgent> Agents => _agents;

    /// <inheritdoc />
    public async Task<Result<AiOrchestrationResult>> RunAsync(AiOrchestrationRequest request, CancellationToken cancellationToken = default)
    {
        var enabled = _agents.Where(a => !request.DisabledAgents.Contains(a.Key)).ToList();
        var run = new AiRunAccumulator();
        var result = request.Connection.SupportsTools
            ? await RunWithToolsAsync(request, enabled, run, cancellationToken)
            : await RunWithRoutingAsync(request, enabled, run, cancellationToken);
        if (!result.Succeeded)
        {
            return Result<AiOrchestrationResult>.From(result);
        }

        var primary = run.Steps.FirstOrDefault(s => s.Kind == AiTraceKinds.Delegation)?.ToolName;
        return Result<AiOrchestrationResult>.Success(new AiOrchestrationResult(result.Value!, primary, run.Steps, run.InputTokens, run.OutputTokens));
    }

    /// <inheritdoc />
    public async Task<Result<string>> ExecuteConfirmedActionAsync(AiActionRequest request, CancellationToken cancellationToken = default)
    {
        var agent = _agents.FirstOrDefault(a => a.Key == request.AgentKey);
        if (agent is null || !agent.ToolNames.Contains(request.ToolName, StringComparer.Ordinal))
        {
            return Result<string>.Invalid("The proposed action is not available for this agent.");
        }

        await using var scope = await CreateAgentScopeAsync(agent, request.UserId, request.UserDisplayName, request.CorrelationId, cancellationToken);
        var tool = scope.Services.GetServices<IMcpTool>().FirstOrDefault(t => t.Name == request.ToolName);
        if (tool is null)
        {
            return Result<string>.Failure("The tool is not registered.");
        }

        if (!McpJson.TryParse(request.ArgumentsJson, out var node) || node is not JsonObject arguments)
        {
            return Result<string>.Invalid("The stored arguments are invalid.");
        }

        var errors = JsonSchemaValidator.Validate(arguments, tool.CreateInputSchema());
        if (errors.Count > 0)
        {
            return Result<string>.Invalid(string.Join(" ", errors));
        }

        var currentUser = scope.Services.GetRequiredService<ICurrentUser>();
        if (tool.RequiredPermission is { } permission && !await currentUser.HasPermissionAsync(permission, cancellationToken))
        {
            return Result<string>.Forbidden($"The {agent.Name} is not allowed to use '{tool.Name}' for you (permission '{permission}').");
        }

        var executed = await ExecuteToolAsync(tool, arguments, cancellationToken);
        return executed.Succeeded
            ? Result<string>.Success(McpJson.ToBoundedText(executed.Value, MaxToolResultChars, out _))
            : Result<string>.From(executed);
    }

    private static AiToolDefinition ToDefinition(IMcpTool tool) => new(tool.Name, tool.Description, tool.CreateInputSchema());

    private static string OrchestratorPrompt(AiOrchestrationRequest request, IReadOnlyList<IAiAgent> agents) =>
        "You are Servora Assistant, the orchestrator of the Servora IT service management platform. You do not access data yourself. " +
        "When the user needs records, knowledge, triage or automation, delegate a precise, self-contained task to the matching specialist " +
        "agent with the delegate_to_* tools (you may delegate to several agents). Answer greetings and general questions directly. " +
        "After receiving agent results, write the final answer for the user: concise, friendly, Markdown allowed (bold, lists, inline code). " +
        "Never invent record numbers or results. If an agent proposed an action that needs confirmation, tell the user to review and " +
        "confirm it with the button below the answer.\n\n" +
        $"Available agents:\n{string.Join("\n", agents.Select(a => $"- {a.Key}: {a.Description}"))}\n\n" +
        $"The user is {request.UserDisplayName}. Current UTC time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}.";

    private static string AgentContext(AiAgentScope scope) =>
        $"\n\nYou run as '{scope.Actor.DisplayName}' with these permissions only: {string.Join(", ", scope.Actor.Permissions ?? new HashSet<string>())}.";

    private async Task<Result<string>> RunWithToolsAsync(AiOrchestrationRequest request, IReadOnlyList<IAiAgent> enabled, AiRunAccumulator run, CancellationToken cancellationToken)
    {
        var tools = enabled.Select(a => new AiToolDefinition(
            DelegatePrefix + a.Key,
            $"Delegate a task to the {a.Name}. {a.Description}",
            JsonNode.Parse("""
                {
                  "type": "object",
                  "properties": { "task": { "type": "string", "description": "Precise, self-contained task including all relevant details from the conversation." } },
                  "required": ["task"]
                }
                """)!.AsObject())).ToList();

        var messages = new List<AiChatMessage>(request.History) { AiChatMessage.User(request.Message) };
        var system = OrchestratorPrompt(request, enabled);
        for (var round = 0; round < MaxOrchestratorRounds; round++)
        {
            var response = await chat.CompleteAsync(request.Connection, new AiChatRequest { SystemPrompt = system, Messages = messages, Tools = tools }, cancellationToken);
            if (!response.Succeeded)
            {
                return Result<string>.From(response);
            }

            run.AddUsage(response.Value!);
            if (response.Value!.ToolCalls.Count == 0)
            {
                return Result<string>.Success(response.Value.Content);
            }

            messages.Add(AiChatMessage.Assistant(response.Value.Content, response.Value.ToolCalls));
            foreach (var call in response.Value.ToolCalls)
            {
                var agent = call.Name.StartsWith(DelegatePrefix, StringComparison.Ordinal)
                    ? enabled.FirstOrDefault(a => a.Key == call.Name[DelegatePrefix.Length..])
                    : null;
                if (agent is null)
                {
                    messages.Add(AiChatMessage.ToolResult(call, $"Unknown or disabled agent '{call.Name}'."));
                    continue;
                }

                var task = McpJson.TryParse(call.ArgumentsJson, out var args) && args is JsonObject o && McpJson.GetString(o, "task") is { } t ? t : request.Message;
                var answer = await DelegateAsync(agent, task, request, run, cancellationToken);
                messages.Add(AiChatMessage.ToolResult(call, answer));
            }
        }

        // Rounds exhausted: ask for a final answer without tools.
        var final = await chat.CompleteAsync(request.Connection, new AiChatRequest { SystemPrompt = system, Messages = messages }, cancellationToken);
        if (!final.Succeeded)
        {
            return Result<string>.From(final);
        }

        run.AddUsage(final.Value!);
        return Result<string>.Success(final.Value!.Content);
    }

    private async Task<Result<string>> RunWithRoutingAsync(AiOrchestrationRequest request, IReadOnlyList<IAiAgent> enabled, AiRunAccumulator run, CancellationToken cancellationToken)
    {
        var agent = AiHeuristicPlanner.Choose(request.Message, enabled);
        if (agent is null)
        {
            var messages = new List<AiChatMessage>(request.History) { AiChatMessage.User(request.Message) };
            var direct = await chat.CompleteAsync(request.Connection, new AiChatRequest { SystemPrompt = OrchestratorPrompt(request, []), Messages = messages }, cancellationToken);
            if (!direct.Succeeded)
            {
                return Result<string>.From(direct);
            }

            run.AddUsage(direct.Value!);
            return Result<string>.Success(direct.Value!.Content);
        }

        return Result<string>.Success(await DelegateAsync(agent, request.Message, request, run, cancellationToken));
    }

    private async Task<string> DelegateAsync(IAiAgent agent, string task, AiOrchestrationRequest request, AiRunAccumulator run, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var delegationIndex = run.Steps.Count;
        run.Steps.Add(new AiTraceStep(AiTraceKinds.Delegation, OrchestratorKey, agent.Key, McpJson.ToNode(new { task }).ToJsonString(), null, AiToolCallStatus.Succeeded, null, "Orchestrator", 0));

        string answer;
        AiToolCallStatus status;
        string? error = null;
        try
        {
            await using var scope = await CreateAgentScopeAsync(agent, request.UserId, request.UserDisplayName, request.CorrelationId, cancellationToken);
            var outcome = request.Connection.SupportsTools
                ? await RunAgentWithToolsAsync(agent, scope, task, request.Connection, run, cancellationToken)
                : await RunAgentWithPrefetchAsync(agent, scope, task, request.Connection, run, cancellationToken);
            (answer, status) = outcome.Succeeded ? (outcome.Value!, AiToolCallStatus.Succeeded) : ($"The {agent.Name} failed: {outcome.Error}", AiToolCallStatus.Failed);
            error = outcome.Succeeded ? null : outcome.Error;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "AI agent {Agent} failed.", agent.Key);
            (answer, status, error) = ($"The {agent.Name} failed unexpectedly.", AiToolCallStatus.Failed, "Unexpected error.");
        }

        run.Steps[delegationIndex] = run.Steps[delegationIndex] with
        {
            ResultText = McpJson.Truncate(answer, MaxToolResultChars),
            Status = status,
            Error = error,
            DurationMs = stopwatch.ElapsedMilliseconds,
        };
        return answer;
    }

    private async Task<Result<string>> RunAgentWithToolsAsync(IAiAgent agent, AiAgentScope scope, string task, AiProviderConnection connection, AiRunAccumulator run, CancellationToken cancellationToken)
    {
        var tools = scope.Services.GetServices<IMcpTool>().Where(t => agent.ToolNames.Contains(t.Name, StringComparer.Ordinal)).ToList();
        var definitions = tools.Select(ToDefinition).ToList();
        var system = agent.SystemPrompt + AgentContext(scope);
        var messages = new List<AiChatMessage> { AiChatMessage.User(task) };

        for (var round = 0; round < MaxAgentRounds; round++)
        {
            var response = await chat.CompleteAsync(connection, new AiChatRequest { SystemPrompt = system, Messages = messages, Tools = definitions }, cancellationToken);
            if (!response.Succeeded)
            {
                return Result<string>.From(response);
            }

            run.AddUsage(response.Value!);
            if (response.Value!.ToolCalls.Count == 0)
            {
                return Result<string>.Success(response.Value.Content);
            }

            messages.Add(AiChatMessage.Assistant(response.Value.Content, response.Value.ToolCalls));
            foreach (var call in response.Value.ToolCalls)
            {
                var tool = tools.FirstOrDefault(t => t.Name == call.Name);
                var resultText = tool is null
                    ? $"Unknown tool '{call.Name}'. Available tools: {string.Join(", ", agent.ToolNames)}."
                    : await RunToolStepAsync(agent, scope, tool, call.ArgumentsJson, run, cancellationToken);
                messages.Add(AiChatMessage.ToolResult(call, resultText));
            }
        }

        var final = await chat.CompleteAsync(connection, new AiChatRequest { SystemPrompt = system, Messages = messages }, cancellationToken);
        if (!final.Succeeded)
        {
            return Result<string>.From(final);
        }

        run.AddUsage(final.Value!);
        return Result<string>.Success(final.Value!.Content);
    }

    private async Task<Result<string>> RunAgentWithPrefetchAsync(IAiAgent agent, AiAgentScope scope, string task, AiProviderConnection connection, AiRunAccumulator run, CancellationToken cancellationToken)
    {
        var context = string.Empty;
        if (agent.BuildPrefetch(task) is { } prefetch)
        {
            var tool = scope.Services.GetServices<IMcpTool>().FirstOrDefault(t => t.Name == prefetch.ToolName && t.IsReadOnly);
            if (tool is not null)
            {
                var text = await RunToolStepAsync(agent, scope, tool, prefetch.Arguments.ToJsonString(), run, cancellationToken);
                context = $"\n\nResult of the tool '{tool.Name}':\n{text}";
            }
        }

        var response = await chat.CompleteAsync(
            connection,
            new AiChatRequest { SystemPrompt = agent.SystemPrompt + AgentContext(scope), Messages = [AiChatMessage.User(task + context)] },
            cancellationToken);
        if (!response.Succeeded)
        {
            return Result<string>.From(response);
        }

        run.AddUsage(response.Value!);
        return Result<string>.Success(response.Value!.Content);
    }

    private async Task<string> RunToolStepAsync(IAiAgent agent, AiAgentScope scope, IMcpTool tool, string argumentsJson, AiRunAccumulator run, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var actorName = scope.Actor.DisplayName;
        var storedArguments = McpJson.Truncate(argumentsJson, 7900);

        if (!McpJson.TryParse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson, out var node) || node is not JsonObject arguments)
        {
            run.Steps.Add(new AiTraceStep(AiTraceKinds.Tool, agent.Key, tool.Name, storedArguments, null, AiToolCallStatus.Failed, "The arguments are not a JSON object.", actorName, 0));
            return "Error: the arguments must be a JSON object.";
        }

        var errors = JsonSchemaValidator.Validate(arguments, tool.CreateInputSchema());
        if (errors.Count > 0)
        {
            var message = string.Join(" ", errors);
            run.Steps.Add(new AiTraceStep(AiTraceKinds.Tool, agent.Key, tool.Name, storedArguments, null, AiToolCallStatus.Failed, McpJson.Truncate(message, 990), actorName, 0));
            return $"Error: invalid arguments. {message}";
        }

        if (tool.IsConsequential)
        {
            run.Steps.Add(new AiTraceStep(AiTraceKinds.Action, agent.Key, tool.Name, arguments.ToJsonString(), null, AiToolCallStatus.PendingConfirmation, null, actorName, 0));
            return PendingActionNote;
        }

        var currentUser = scope.Services.GetRequiredService<ICurrentUser>();
        if (tool.RequiredPermission is { } permission && !await currentUser.HasPermissionAsync(permission, cancellationToken))
        {
            var denied = $"Permission '{permission}' is not available to the {agent.Name} for this user.";
            run.Steps.Add(new AiTraceStep(AiTraceKinds.Tool, agent.Key, tool.Name, storedArguments, null, AiToolCallStatus.Denied, denied, actorName, stopwatch.ElapsedMilliseconds));
            return $"Error: {denied}";
        }

        var result = await ExecuteToolAsync(tool, arguments, cancellationToken);
        if (!result.Succeeded)
        {
            var status = result.ErrorKind == ResultErrorKind.Forbidden ? AiToolCallStatus.Denied : AiToolCallStatus.Failed;
            run.Steps.Add(new AiTraceStep(AiTraceKinds.Tool, agent.Key, tool.Name, storedArguments, null, status, McpJson.Truncate(result.Error ?? "Failed.", 990), actorName, stopwatch.ElapsedMilliseconds));
            return $"Error: {result.Error}";
        }

        var text = McpJson.ToBoundedText(result.Value, MaxToolResultChars, out _);
        run.Steps.Add(new AiTraceStep(AiTraceKinds.Tool, agent.Key, tool.Name, storedArguments, text, AiToolCallStatus.Succeeded, null, actorName, stopwatch.ElapsedMilliseconds));
        return text;
    }

    private async Task<Result<JsonNode>> ExecuteToolAsync(IMcpTool tool, JsonObject arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(McpLimits.ToolTimeout);
        try
        {
            return await tool.ExecuteAsync(arguments, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<JsonNode>.Failure("The tool timed out.");
        }
    }

    private async Task<AiAgentScope> CreateAgentScopeAsync(IAiAgent agent, Guid userId, string userDisplayName, string? correlationId, CancellationToken cancellationToken)
    {
        var userPermissions = await permissions.GetUserPermissionsAsync(userId, cancellationToken);
        var effective = AiPermissionIntersection.Intersect(agent.Scope, userPermissions);
        var actor = new ActorInfo(ActorType.Agent, $"agent:{agent.Key}", $"{agent.Name} (for {userDisplayName})", userId, effective);
        var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(actor, correlationId);
        return new AiAgentScope(scope, actor);
    }
}
