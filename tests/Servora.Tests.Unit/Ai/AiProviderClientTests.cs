using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Ai;
using Servora.Domain.Ai;
using Servora.Infrastructure.Ai;
using Servora.Tests.Unit.Ai.Fakes;

namespace Servora.Tests.Unit.Ai;

/// <summary>Tests of the HTTP provider clients (OpenAI-compatible, Anthropic, Azure OpenAI) against a stub endpoint.</summary>
public sealed class AiProviderClientTests
{
    private const string OpenAiAnswer = """
        {
          "id": "chatcmpl-1",
          "choices": [ { "index": 0, "finish_reason": "stop", "message": { "role": "assistant", "content": "Hello from the model." } } ],
          "usage": { "prompt_tokens": 12, "completion_tokens": 4 }
        }
        """;

    private static readonly AiChatRequest s_request = new()
    {
        SystemPrompt = "You are helpful.",
        Messages = [AiChatMessage.User("Hi")],
        Tools = [new AiToolDefinition("search_records", "Search records.", JsonNode.Parse("""{ "type": "object" }""")!.AsObject())],
    };

    [Fact]
    public async Task OpenAiCompatible_SendsChatCompletionAndParsesTheAnswer()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, OpenAiAnswer);
        var client = new OpenAiCompatibleClient(Transport(handler));

        var result = await client.CompleteAsync(Connection(AiProviderKind.OpenAiCompatible, "http://localhost:11434/v1/"), s_request, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Hello from the model.", result.Value!.Content);
        Assert.Equal(12, result.Value.InputTokens);
        Assert.Equal(4, result.Value.OutputTokens);
        Assert.Equal("http://localhost:11434/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test-123456", handler.Request.Headers.Authorization.Parameter);

        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("llama3.1", body["model"]!.GetValue<string>());
        Assert.Equal("system", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("user", body["messages"]![1]!["role"]!.GetValue<string>());
        Assert.Equal("search_records", body["tools"]![0]!["function"]!["name"]!.GetValue<string>());
        Assert.Equal(512, body["max_tokens"]!.GetValue<int>());
    }

    [Fact]
    public async Task OpenAiCompatible_ParsesToolCalls()
    {
        const string answer = """
            { "choices": [ { "finish_reason": "tool_calls", "message": { "role": "assistant", "content": null,
              "tool_calls": [ { "id": "call_1", "type": "function", "function": { "name": "search_records", "arguments": "{\"table\":\"incident\"}" } } ] } } ] }
            """;
        var client = new OpenAiCompatibleClient(Transport(new StubHttpHandler(HttpStatusCode.OK, answer)));

        var result = await client.CompleteAsync(Connection(AiProviderKind.OpenAiCompatible, "http://localhost:1234/v1"), s_request, CancellationToken.None);

        var call = Assert.Single(result.Value!.ToolCalls);
        Assert.Equal("call_1", call.Id);
        Assert.Equal("search_records", call.Name);
        Assert.Equal("""{"table":"incident"}""", call.ArgumentsJson);
    }

    [Fact]
    public async Task ProviderErrors_AreReportedWithoutLeakingTheApiKey()
    {
        var handler = new StubHttpHandler(HttpStatusCode.Unauthorized, """{ "error": { "message": "Incorrect API key provided: sk-test-123456" } }""");
        var client = new OpenAiCompatibleClient(Transport(handler));

        var result = await client.CompleteAsync(Connection(AiProviderKind.OpenAiCompatible, "http://localhost:1234/v1"), s_request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("401", result.Error);
        Assert.DoesNotContain("sk-test-123456", result.Error);
    }

    [Fact]
    public async Task UnexpectedResponses_AreFailures()
    {
        var client = new OpenAiCompatibleClient(Transport(new StubHttpHandler(HttpStatusCode.OK, """{ "unexpected": true }""")));

        var result = await client.CompleteAsync(Connection(AiProviderKind.OpenAiCompatible, "http://localhost:1234/v1"), s_request, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Anthropic_UsesMessagesApiHeadersAndParsesToolUse()
    {
        const string answer = """
            { "content": [ { "type": "text", "text": "Let me check." }, { "type": "tool_use", "id": "toolu_1", "name": "search_records", "input": { "table": "incident" } } ],
              "stop_reason": "tool_use", "usage": { "input_tokens": 30, "output_tokens": 9 } }
            """;
        var handler = new StubHttpHandler(HttpStatusCode.OK, answer);
        var client = new AnthropicClient(Transport(handler));

        var result = await client.CompleteAsync(Connection(AiProviderKind.Anthropic, "https://api.anthropic.com"), s_request, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Let me check.", result.Value!.Content);
        Assert.Equal("search_records", Assert.Single(result.Value.ToolCalls).Name);
        Assert.Equal(30, result.Value.InputTokens);
        Assert.Equal("https://api.anthropic.com/v1/messages", handler.Request!.RequestUri!.ToString());
        Assert.Equal("sk-test-123456", handler.Request.Headers.GetValues("x-api-key").Single());
        Assert.Equal(AnthropicWireFormat.ApiVersion, handler.Request.Headers.GetValues("anthropic-version").Single());
        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("You are helpful.", body["system"]!.GetValue<string>());
        Assert.NotNull(body["tools"]![0]!["input_schema"]);
    }

    [Fact]
    public async Task AzureOpenAi_AddressesTheDeploymentWithApiKeyHeader()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, OpenAiAnswer);
        var client = new AzureOpenAiClient(Transport(handler));
        var connection = Connection(AiProviderKind.AzureOpenAi, "https://contoso.openai.azure.com") with { Deployment = "gpt-4o", ApiVersion = "2024-10-21" };

        var result = await client.CompleteAsync(connection, s_request, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("https://contoso.openai.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2024-10-21", handler.Request!.RequestUri!.ToString());
        Assert.Equal("sk-test-123456", handler.Request.Headers.GetValues("api-key").Single());
    }

    [Fact]
    public async Task ChatClient_DispatchesByProviderKind()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, OpenAiAnswer);
        var chat = new AiChatClient([new OpenAiCompatibleClient(Transport(handler))]);

        var ok = await chat.CompleteAsync(Connection(AiProviderKind.OpenAiCompatible, "http://localhost:1234/v1"), s_request);
        var missing = await chat.CompleteAsync(Connection(AiProviderKind.Anthropic, "https://api.anthropic.com"), s_request);

        Assert.True(ok.Succeeded);
        Assert.False(missing.Succeeded);
    }

    private static AiHttpTransport Transport(StubHttpHandler handler) => new(new StubHttpClientFactory(handler), NullLogger<AiHttpTransport>.Instance);

    private static AiProviderConnection Connection(AiProviderKind kind, string baseUrl) => new()
    {
        Name = "Test",
        Kind = kind,
        BaseUrl = baseUrl,
        Model = "llama3.1",
        ApiKey = "sk-test-123456",
        MaxTokens = 512,
        Temperature = 0.2,
    };
}
