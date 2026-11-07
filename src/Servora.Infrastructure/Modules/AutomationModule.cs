using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Automation &amp; Integration (T-08): flows, scheduler, REST data sources, import sets module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class AutomationModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAutomationModule(this IServiceCollection services)
    {
        return services;
    }
}
