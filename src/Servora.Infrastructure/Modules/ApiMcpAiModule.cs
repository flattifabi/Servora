using Microsoft.Extensions.DependencyInjection;

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
        return services;
    }
}
