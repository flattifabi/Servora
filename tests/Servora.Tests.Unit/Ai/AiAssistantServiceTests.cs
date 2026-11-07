using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Ai;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Security;
using Servora.Domain.Ai;
using Servora.Infrastructure.Ai;
using Servora.Tests.Unit.Ai.Fakes;
using Servora.Tests.Unit.Api.Fakes;

namespace Servora.Tests.Unit.Ai;

/// <summary>
/// End-to-end tests of the assistant on SQLite: provider configuration (key in the secret store), answers through an
/// OpenAI-compatible endpoint, persisted traces and one-time confirmation of proposed actions.
/// </summary>
public sealed class AiAssistantServiceTests : IAsyncLifetime
{
    private readonly InMemorySecretStore _secrets = new();
    private readonly RecordingAuditService _audit = new();
    private readonly TestClock _clock = new();
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Assistant_AnswersThroughAConfiguredOpenAiCompatibleEndpoint()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, """
            { "choices": [ { "finish_reason": "stop", "message": { "role": "assistant", "content": "Hi! How can I help?" } } ],
              "usage": { "prompt_tokens": 42, "completion_tokens": 7 } }
            """);
        var chat = new AiChatClient([new OpenAiCompatibleClient(new AiHttpTransport(new StubHttpClientFactory(handler), NullLogger<AiHttpTransport>.Instance))]);
        await using var services = new AiTestServices(chat);
        var user = new TestCurrentUser { UserId = services.UserId };
        var resolver = new AiConnectionResolver(_db, _secrets);
        var configuration = new AiConfigurationService(_db, user, _secrets, resolver, chat, services.Orchestrator, Array.Empty<IMcpTool>(), _audit, _clock);

        var saved = await configuration.SaveProviderAsync(new AiProviderSaveRequest
        {
            Name = "Local Ollama",
            Kind = AiProviderKind.OpenAiCompatible,
            BaseUrl = "http://localhost:11434/v1",
            Model = "llama3.1",
            ApiKey = "sk-local-secret",
        });
        Assert.True(saved.Succeeded, saved.Error);

        // The key lives in the secret store only and is never returned.
        var providers = (await configuration.ListProvidersAsync()).Value!;
        var provider = Assert.Single(providers);
        Assert.True(provider.HasApiKey);
        Assert.True(provider.IsDefault);
        Assert.DoesNotContain("sk-local-secret", System.Text.Json.JsonSerializer.Serialize(providers), StringComparison.Ordinal);
        Assert.Contains("sk-local-secret", _secrets.Values.Values);

        var assistant = CreateAssistant(user, resolver, services);
        var status = (await assistant.GetStatusAsync()).Value!;
        Assert.True(status.HasProvider);

        var sent = await assistant.SendAsync(null, "Hello");

        Assert.True(sent.Succeeded, sent.Error);
        var reply = sent.Value!.Messages.Last();
        Assert.Equal(AiMessageRole.Assistant, reply.Role);
        Assert.Equal("Hi! How can I help?", reply.Content);
        Assert.False(reply.IsError);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("sk-local-secret", handler.Request.Headers.Authorization.Parameter);
        Assert.Equal("Hello", sent.Value.Title);

        await using var db = _db.Create();
        var stored = await db.Set<AiMessage>().Where(m => m.Role == AiMessageRole.Assistant).SingleAsync();
        Assert.Equal(42, stored.InputTokens);
        Assert.Equal(7, stored.OutputTokens);
    }

    [Fact]
    public async Task Assistant_WithoutProvider_ShowsAGuidedErrorMessage()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("unused")));
        var user = new TestCurrentUser { UserId = services.UserId };
        var assistant = CreateAssistant(user, new AiConnectionResolver(_db, _secrets), services);

        Assert.False((await assistant.GetStatusAsync()).Value!.HasProvider);
        var sent = await assistant.SendAsync(null, "Hello");

        Assert.True(sent.Succeeded);
        Assert.True(sent.Value!.Messages.Last().IsError);
        Assert.Contains("No AI provider", sent.Value.Messages.Last().Content);
    }

    [Fact]
    public async Task ProposedActions_AreExecutedExactlyOnceAfterConfirmation()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((request, index) => index switch
        {
            0 => ScriptedChatClient.Call("delegate_to_triage", """{ "task": "Open an incident for the broken printer" }"""),
            1 => ScriptedChatClient.Call("create_incident", """{ "short_description": "Printer on floor 3 broken" }"""),
            2 => ScriptedChatClient.Text("Proposed."),
            _ => ScriptedChatClient.Text("Please confirm the incident below."),
        }));
        services.Permissions.Set(services.UserId, Permissions.All);
        var user = new TestCurrentUser { UserId = services.UserId };
        var assistant = CreateAssistant(user, await ConfigureFakeProviderAsync(), services);

        var sent = await assistant.SendAsync(null, "The printer on floor 3 is broken, please open an incident");
        Assert.True(sent.Succeeded, sent.Error);
        var reply = sent.Value!.Messages.Last();
        Assert.Equal("triage", reply.AgentKey);
        var pending = Assert.Single(reply.Steps, s => s.Status == AiToolCallStatus.PendingConfirmation);
        Assert.Empty(services.CreateIncident.Calls);

        _clock.UtcNow = _clock.UtcNow.AddSeconds(5);
        var first = await assistant.ConfirmActionAsync(pending.Id);
        var second = await assistant.ConfirmActionAsync(pending.Id);
        var reject = await assistant.RejectActionAsync(pending.Id);

        Assert.True(first.Succeeded, first.Error);
        Assert.Equal(ResultErrorKind.NotFound, second.ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, reject.ErrorKind);
        Assert.Single(services.CreateIncident.Calls);
        Assert.Contains("INC0001002", first.Value!.Messages.Last().Content);
        Assert.Contains(first.Value.Messages.SelectMany(m => m.Steps), s => s.Id == pending.Id && s.Status == AiToolCallStatus.Confirmed);
        Assert.Contains(_audit.Entries, e => e.Action == "action.confirmed");
    }

    [Fact]
    public async Task RejectedActions_AreNeverExecuted()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((request, index) => index switch
        {
            0 => ScriptedChatClient.Call("delegate_to_triage", """{ "task": "Open an incident" }"""),
            1 => ScriptedChatClient.Call("create_incident", """{ "short_description": "Printer broken" }"""),
            _ => ScriptedChatClient.Text("Please confirm."),
        }));
        services.Permissions.Set(services.UserId, Permissions.All);
        var user = new TestCurrentUser { UserId = services.UserId };
        var assistant = CreateAssistant(user, await ConfigureFakeProviderAsync(), services);
        var pending = (await assistant.SendAsync(null, "open an incident")).Value!.Messages.Last().Steps.Single(s => s.Status == AiToolCallStatus.PendingConfirmation);

        Assert.True((await assistant.RejectActionAsync(pending.Id)).Succeeded);
        Assert.Equal(ResultErrorKind.NotFound, (await assistant.ConfirmActionAsync(pending.Id)).ErrorKind);
        Assert.Empty(services.CreateIncident.Calls);
    }

    [Fact]
    public async Task Conversations_ArePrivateToTheirOwner()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("Hello!")));
        var owner = new TestCurrentUser { UserId = services.UserId };
        var resolver = await ConfigureFakeProviderAsync();
        var conversation = (await CreateAssistant(owner, resolver, services).SendAsync(null, "Hi")).Value!;

        var stranger = CreateAssistant(new TestCurrentUser(), resolver, services);

        Assert.Equal(ResultErrorKind.NotFound, (await stranger.GetConversationAsync(conversation.Id)).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await stranger.SendAsync(conversation.Id, "hijack")).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await stranger.DeleteConversationAsync(conversation.Id)).ErrorKind);
        Assert.Empty((await stranger.ListConversationsAsync()).Value!);
        Assert.Single((await CreateAssistant(owner, resolver, services).ListConversationsAsync()).Value!);
    }

    [Fact]
    public async Task Assistant_RequiresTheUsePermissionAndBoundsMessages()
    {
        await using var services = new AiTestServices(new ScriptedChatClient((_, _) => ScriptedChatClient.Text("Hello!")));
        var user = new TestCurrentUser { UserId = services.UserId };
        var assistant = CreateAssistant(user, await ConfigureFakeProviderAsync(), services);

        Assert.Equal(ResultErrorKind.Validation, (await assistant.SendAsync(null, new string('x', AiAssistantService.MaxMessageLength + 1))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await assistant.SendAsync(null, "   ")).ErrorKind);

        user.Grant(Permissions.Incidents.Read);
        Assert.Equal(ResultErrorKind.Forbidden, (await assistant.SendAsync(null, "Hi")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await assistant.ListConversationsAsync()).ErrorKind);
    }

    private AiAssistantService CreateAssistant(TestCurrentUser user, IAiConnectionResolver resolver, AiTestServices services) =>
        new(_db, user, resolver, services.Orchestrator, [services.SearchRecords, services.CreateIncident, services.SearchKnowledge], _audit, _clock);

    private async Task<IAiConnectionResolver> ConfigureFakeProviderAsync()
    {
        await using var db = _db.Create();
        db.Set<AiProviderConfig>().Add(new AiProviderConfig { Name = "Fake", Kind = AiProviderKind.OpenAiCompatible, BaseUrl = "http://localhost:1/v1", Model = "fake", IsDefault = true, IsEnabled = true, SupportsTools = true });
        await db.SaveChangesAsync();
        return new AiConnectionResolver(_db, _secrets);
    }
}
