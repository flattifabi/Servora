using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Ai;
using Servora.Application.Ai.Agents;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Security;
using Servora.Tests.Unit.Api.Fakes;
using Servora.Tests.Unit.Mcp.Fakes;

namespace Servora.Tests.Unit.Ai.Fakes;

/// <summary>
/// Service provider for orchestrator tests: scoped <see cref="ActorContext"/> and actor-aware <see cref="ICurrentUser"/>
/// (so tools see the agent identity), fake tools named like the real ones, the built-in agents and a scripted provider.
/// </summary>
internal sealed class AiTestServices : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IAiChatClient _chat;

    /// <summary>Initializes a new instance of the <see cref="AiTestServices"/> class.</summary>
    /// <param name="chat">The provider (usually a <see cref="ScriptedChatClient"/>).</param>
    public AiTestServices(IAiChatClient chat)
    {
        _chat = chat;
        var services = new ServiceCollection();
        services.AddScoped<ActorContext>();
        services.AddScoped<ICurrentUser>(sp => new TestCurrentUser(sp.GetRequiredService<ActorContext>()));
        foreach (var tool in new[] { SearchRecords, CreateIncident, SearchKnowledge })
        {
            services.AddSingleton<IMcpTool>(tool);
        }

        _provider = services.BuildServiceProvider();
        Orchestrator = new AiOrchestrator(
            chat,
            [new TriageAgent(), new KnowledgeAgent(), new RecordAgent()],
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Permissions,
            NullLogger<AiOrchestrator>.Instance);
    }

    /// <summary>Gets the requesting user id.</summary>
    public Guid UserId { get; } = Guid.CreateVersion7();

    /// <summary>Gets the scripted fake provider (only when the services were created with one).</summary>
    public ScriptedChatClient Chat => (ScriptedChatClient)_chat;

    /// <summary>Gets the user permissions.</summary>
    public TestPermissionService Permissions { get; } = new();

    /// <summary>Gets the fake <c>search_records</c> tool (requires <c>incident.read</c>).</summary>
    public FakeMcpTool SearchRecords { get; } = new("search_records", Servora.Application.Security.Permissions.Incidents.Read)
    {
        SchemaJson = """{ "type": "object", "properties": { "table": { "type": "string" }, "query": { "type": "string" } }, "required": ["table"] }""",
        Handler = _ => Result<System.Text.Json.Nodes.JsonNode>.Success(System.Text.Json.Nodes.JsonNode.Parse("""{ "records": [ { "number": "INC0001001", "state": "New" } ] }""")!),
    };

    /// <summary>Gets the fake <c>create_incident</c> tool (consequential, requires <c>incident.create</c>).</summary>
    public FakeMcpTool CreateIncident { get; } = new("create_incident", Servora.Application.Security.Permissions.Incidents.Create, consequential: true)
    {
        SchemaJson = """{ "type": "object", "properties": { "short_description": { "type": "string", "minLength": 3 } }, "required": ["short_description"] }""",
        Handler = _ => Result<System.Text.Json.Nodes.JsonNode>.Success(System.Text.Json.Nodes.JsonNode.Parse("""{ "number": "INC0001002", "message": "Incident INC0001002 created." }""")!),
    };

    /// <summary>Gets the fake <c>search_knowledge</c> tool (requires <c>knowledge.read</c>).</summary>
    public FakeMcpTool SearchKnowledge { get; } = new("search_knowledge", Servora.Application.Security.Permissions.Knowledge.Read)
    {
        SchemaJson = """{ "type": "object", "properties": { "query": { "type": "string" }, "limit": { "type": "integer" } }, "required": ["query"] }""",
        Handler = _ => Result<System.Text.Json.Nodes.JsonNode>.Success(System.Text.Json.Nodes.JsonNode.Parse("""{ "articles": [ { "number": "KB0001", "title": "Reset VPN" } ] }""")!),
    };

    /// <summary>Gets the orchestrator under test.</summary>
    public AiOrchestrator Orchestrator { get; }

    /// <summary>Builds a request for the test user.</summary>
    /// <param name="message">The message.</param>
    /// <param name="supportsTools">Whether the provider supports tool calling.</param>
    /// <returns>The request.</returns>
    public AiOrchestrationRequest Request(string message, bool supportsTools = true) =>
        new(UserId, "Uma User", [], message, new AiProviderConnection { Name = "Fake", BaseUrl = "http://localhost:11434/v1", Model = "fake", SupportsTools = supportsTools });

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
