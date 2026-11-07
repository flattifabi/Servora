using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Self-service portal (T-11) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class PortalModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPortalModule(this IServiceCollection services)
    {
        return services;
    }
}
