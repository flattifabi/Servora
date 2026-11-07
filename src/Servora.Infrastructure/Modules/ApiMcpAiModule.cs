using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Ai;
using Servora.Application.Ai.Agents;
using Servora.Application.Api;
using Servora.Application.Mcp;
using Servora.Application.Mcp.Tools;
using Servora.Infrastructure.Ai;
using Servora.Infrastructure.Api;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the API keys, REST API, MCP server and AI orchestration (T-10) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ApiMcpAiModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiMcpAiModule(this IServiceCollection services)
    {
        // API keys
        services.AddSingleton<ApiKeyUsageTracker>();
        services.AddSingleton<ApiKeyRateLimitRegistry>();
        services.AddScoped<IApiKeyAuthenticator, ApiKeyAuthenticator>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddHostedService<ApiUsageMaintenanceService>();

        // MCP server and tools (tools are also used by the AI sub-agents)
        services.AddScoped<IMcpTool, SearchRecordsTool>();
        services.AddScoped<IMcpTool, GetRecordTool>();
        services.AddScoped<IMcpTool, CreateIncidentTool>();
        services.AddScoped<IMcpTool, SearchKnowledgeTool>();
        services.AddScoped<IMcpTool, ListDataSourcesTool>();
        services.AddScoped<IMcpTool, ExecuteDataSourceTool>();
        services.AddScoped<IMcpTool, ListFlowsTool>();
        services.AddScoped<IMcpTool, StartFlowTool>();
        services.AddScoped<IMcpTool, GetFlowRunTool>();
        services.AddScoped<IMcpTool, PlatformHealthTool>();
        services.AddScoped<IMcpRuntimeStateProvider, McpRuntimeStateProvider>();
        services.AddScoped<IMcpInvocationLog, McpInvocationLog>();
        services.AddScoped<McpResourceCatalog>();
        services.AddScoped<IMcpServer, McpServer>();
        services.AddScoped<IMcpSettingsService, McpSettingsService>();

        // AI providers (plain HttpClient, no SDKs), agents, orchestrator and assistant
        services.AddHttpClient(AiHttpTransport.ClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan; // per-request timeouts come from the provider configuration
                client.MaxResponseContentBufferSize = AiHttpTransport.MaxResponseBytes;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Servora-AI/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });
        services.AddSingleton<AiHttpTransport>();
        services.AddSingleton<IAiProviderClient, OpenAiCompatibleClient>();
        services.AddSingleton<IAiProviderClient, AnthropicClient>();
        services.AddSingleton<IAiProviderClient, AzureOpenAiClient>();
        services.AddSingleton<IAiChatClient, AiChatClient>();
        services.AddSingleton<IAiAgent, TriageAgent>();
        services.AddSingleton<IAiAgent, KnowledgeAgent>();
        services.AddSingleton<IAiAgent, RecordAgent>();
        services.AddSingleton<IAiAgent, AutomationAgent>();
        services.AddScoped<IAiConnectionResolver, AiConnectionResolver>();
        services.AddScoped<IAiOrchestrator, AiOrchestrator>();
        services.AddScoped<IAiConfigurationService, AiConfigurationService>();
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        return services;
    }
}
