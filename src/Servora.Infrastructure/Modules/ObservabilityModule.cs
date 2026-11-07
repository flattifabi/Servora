using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Observability &amp; Analytics (T-09): logging, debug switch, audit viewer, health, dashboards, reports module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ObservabilityModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddObservabilityModule(this IServiceCollection services)
    {
        return services;
    }
}
