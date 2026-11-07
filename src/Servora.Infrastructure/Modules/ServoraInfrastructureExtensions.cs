using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Entry point that registers the core kernel and every module.</summary>
public static class ServoraInfrastructureExtensions
{
    /// <summary>Registers all Servora application and infrastructure services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServoraInfrastructure(this IServiceCollection services)
    {
        services.AddCoreModule();
        services.AddIdentityModule();
        services.AddConfigurationModule();
        services.AddItsmModule();
        services.AddServiceManagementModule();
        services.AddKnowledgeCmdbModule();
        services.AddPlatformModule();
        services.AddAutomationModule();
        services.AddObservabilityModule();
        services.AddApiMcpAiModule();
        services.AddPortalModule();
        return services;
    }
}
