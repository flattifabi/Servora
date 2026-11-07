using Servora.Application.Ai;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Ai;
using Servora.Tests.Unit.Ai.Fakes;

namespace Servora.Tests.Unit.Ai;

/// <summary>Tests of <see cref="AiOrchestrator"/> delegation, agent identities and confirmations with a fake provider.</summary>
public sealed class AiOrchestratorTests
{
    [Fact]
    public async Task ToolCalling_DelegatesToTheRecordAgentWhichUsesItsTool()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((request, index) => index switch
        {
            0 => ScriptedChatClient.Call("delegate_to_records", """{ "task": "Find INC0001001" }"""),
            1 => ScriptedChatClient.Call("search_records", """{ "table": "incident", "query": "INC0001001" }"""),
            2 => ScriptedChatClient.Text("INC0001001 is New."),
            _ => ScriptedChatClient.Text("**INC0001001** is in state New."),
        }));
        services.Permissions.Set(services.UserId, Permissions.Incidents.Read, Permissions.Platform.ReadRecords);

        var result = await services.Orchestrator.RunAsync(services.Request("What is the status of INC0001001?"));

        Assert.True(result.Succeeded, result.Error);
        var value = result.Value!;
        Assert.Equal("**INC0001001** is in state New.", value.Answer);
        Assert.Equal("records", value.AgentKey);
        Assert.Single(services.SearchRecords.Calls);

        var delegation = Assert.Single(value.Steps, s => s.Kind == AiTraceKinds.Delegation);
        Assert.Equal(AiOrchestrator.OrchestratorKey, delegation.AgentKey);
        Assert.Equal("records", delegation.ToolName);
        Assert.Equal(AiToolCallStatus.Succeeded, delegation.Status);
        Assert.Equal("INC0001001 is New.", delegation.ResultText);

        var tool = Assert.Single(value.Steps, s => s.Kind == AiTraceKinds.Tool);
        Assert.Equal("records", tool.AgentKey);
        Assert.Equal("search_records", tool.ToolName);
        Assert.Equal(AiToolCallStatus.Succeeded, tool.Status);
        Assert.Contains("Record Agent", tool.ActorName);
        Assert.Contains("Uma User", tool.ActorName);

        // The orchestrator only sees delegation tools, the agent only its own tools.
        Assert.All(services.Chat.Requests[0].Tools, t => Assert.StartsWith(AiOrchestrator.DelegatePrefix, t.Name));
        Assert.Equal(["search_records"], services.Chat.Requests[1].Tools.Select(t => t.Name));
        Assert.Equal(20 + 20 + 10 + 10, value.InputTokens);
        Assert.Equal(8 + 8 + 5 + 5, value.OutputTokens);
    }

    [Fact]
    public async Task ConsequentialTools_AreProposedButNeverExecuted()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((request, index) => index switch
        {
            0 => ScriptedChatClient.Call("delegate_to_triage", """{ "task": "Create an incident: printer on floor 3 is broken" }"""),
            1 => ScriptedChatClient.Call("create_incident", """{ "short_description": "Printer on floor 3 broken" }"""),
            2 => ScriptedChatClient.Text("I proposed an incident."),
            _ => ScriptedChatClient.Text("Please confirm the incident below."),
        }));
        services.Permissions.Set(services.UserId, Permissions.Incidents.Read, Permissions.Incidents.Create);

        var result = await services.Orchestrator.RunAsync(services.Request("The printer on floor 3 is broken, please open an incident."));

        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(services.CreateIncident.Calls);
        var action = Assert.Single(result.Value!.Steps, s => s.Kind == AiTraceKinds.Action);
        Assert.Equal(AiToolCallStatus.PendingConfirmation, action.Status);
        Assert.Equal("triage", action.AgentKey);
        Assert.Contains("Printer on floor 3", action.ArgumentsJson);

        // The tool result fed back to the agent states that nothing was executed.
        var agentFollowUp = services.Chat.Requests[2];
        Assert.Contains(agentFollowUp.Messages, m => m.Role == AiChatRole.Tool && m.Content.Contains("NOT executed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteConfirmedAction_RunsTheToolUnderTheAgentIdentity()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("unused")));
        services.Permissions.Set(services.UserId, Permissions.Incidents.Create);

        var result = await services.Orchestrator.ExecuteConfirmedActionAsync(
            new AiActionRequest(services.UserId, "Uma User", "triage", "create_incident", """{ "short_description": "Printer broken" }"""));

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains("INC0001002", result.Value);
        Assert.Single(services.CreateIncident.Calls);
    }

    [Fact]
    public async Task ExecuteConfirmedAction_NeverExceedsTheUsersPermissions()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("unused")));
        services.Permissions.Set(services.UserId, Permissions.Incidents.Read);

        var result = await services.Orchestrator.ExecuteConfirmedActionAsync(
            new AiActionRequest(services.UserId, "Uma User", "triage", "create_incident", """{ "short_description": "Printer broken" }"""));

        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
        Assert.Empty(services.CreateIncident.Calls);
    }

    [Fact]
    public async Task ExecuteConfirmedAction_RejectsToolsOutsideTheAgentAndInvalidArguments()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("unused")));
        services.Permissions.Set(services.UserId, Permissions.All);

        var foreign = await services.Orchestrator.ExecuteConfirmedActionAsync(new AiActionRequest(services.UserId, "Uma", "knowledge", "create_incident", "{}"));
        var invalid = await services.Orchestrator.ExecuteConfirmedActionAsync(new AiActionRequest(services.UserId, "Uma", "triage", "create_incident", """{ "short_description": "x" }"""));

        Assert.Equal(ResultErrorKind.Validation, foreign.ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, invalid.ErrorKind);
        Assert.Empty(services.CreateIncident.Calls);
    }

    [Fact]
    public async Task AgentWithoutUserPermission_GetsDeniedToolResult()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((request, index) => index switch
        {
            0 => ScriptedChatClient.Call("delegate_to_knowledge", """{ "task": "How do I reset the VPN?" }"""),
            1 => ScriptedChatClient.Call("search_knowledge", """{ "query": "reset vpn" }"""),
            2 => ScriptedChatClient.Text("I cannot access the knowledge base for you."),
            _ => ScriptedChatClient.Text("Sorry, you lack access to knowledge articles."),
        }));
        services.Permissions.Set(services.UserId, Permissions.Incidents.Read);

        var result = await services.Orchestrator.RunAsync(services.Request("How do I reset the VPN?"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(services.SearchKnowledge.Calls);
        var tool = Assert.Single(result.Value!.Steps, s => s.Kind == AiTraceKinds.Tool);
        Assert.Equal(AiToolCallStatus.Denied, tool.Status);
        Assert.Contains(Permissions.Knowledge.Read, tool.Error);
    }

    [Fact]
    public async Task InvalidToolArguments_AreReportedToTheAgentWithoutExecution()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((request, index) => index switch
        {
            0 => ScriptedChatClient.Call("delegate_to_records", """{ "task": "list incidents" }"""),
            1 => ScriptedChatClient.Call("search_records", """{ "query": 5 }"""),
            2 => ScriptedChatClient.Text("Could not search."),
            _ => ScriptedChatClient.Text("Done."),
        }));
        services.Permissions.Set(services.UserId, Permissions.All);

        var result = await services.Orchestrator.RunAsync(services.Request("list incidents"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(services.SearchRecords.Calls);
        var tool = Assert.Single(result.Value!.Steps, s => s.Kind == AiTraceKinds.Tool);
        Assert.Equal(AiToolCallStatus.Failed, tool.Status);
        Assert.Contains(services.Chat.Requests[2].Messages, m => m.Role == AiChatRole.Tool && m.Content.StartsWith("Error: invalid arguments", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisabledAgents_AreNotOfferedToTheModel()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("Hello!")));
        var request = services.Request("Hi") with { DisabledAgents = new HashSet<string> { "knowledge" } };

        var result = await services.Orchestrator.RunAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("Hello!", result.Value!.Answer);
        Assert.Null(result.Value.AgentKey);
        Assert.DoesNotContain(services.Chat.Requests[0].Tools, t => t.Name == "delegate_to_knowledge");
        Assert.Contains(services.Chat.Requests[0].Tools, t => t.Name == "delegate_to_records");
    }

    [Fact]
    public async Task UnknownDelegationTarget_IsReportedBackToTheModel()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((request, index) => index switch
        {
            0 => ScriptedChatClient.Call("delegate_to_root", """{ "task": "sudo" }"""),
            _ => ScriptedChatClient.Text("I cannot do that."),
        }));

        var result = await services.Orchestrator.RunAsync(services.Request("do something"));

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(result.Value!.Steps, s => s.Kind == AiTraceKinds.Delegation);
        Assert.Contains(services.Chat.Requests[1].Messages, m => m.Role == AiChatRole.Tool && m.Content.Contains("Unknown or disabled agent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProviderFailure_IsReturnedAsFailure()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => Result<AiChatResponse>.Failure("HTTP 401: invalid API key")));

        var result = await services.Orchestrator.RunAsync(services.Request("Hi"));

        Assert.False(result.Succeeded);
        Assert.Contains("401", result.Error);
    }

    [Fact]
    public async Task WithoutToolCalling_RoutesByKeywordsAndPrefetchesReadOnlyData()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("INC0001001 is New.")));
        services.Permissions.Set(services.UserId, Permissions.Incidents.Read, Permissions.Platform.ReadRecords);

        var result = await services.Orchestrator.RunAsync(services.Request("Show me INC0001001", supportsTools: false));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("records", result.Value!.AgentKey);
        var call = Assert.Single(services.SearchRecords.Calls);
        Assert.Equal("INC0001001", call["query"]!.GetValue<string>());
        var request = Assert.Single(services.Chat.Requests);
        Assert.Empty(request.Tools);
        Assert.Contains("INC0001001", request.Messages[0].Content);
        Assert.Contains("\"records\"", request.Messages[0].Content);
    }

    [Fact]
    public async Task WithoutToolCalling_GeneralQuestionsAreAnsweredDirectly()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("Hello Uma!")));

        var result = await services.Orchestrator.RunAsync(services.Request("Good morning", supportsTools: false));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.AgentKey);
        Assert.Empty(result.Value.Steps);
        Assert.Equal("Hello Uma!", result.Value.Answer);
    }
}
