using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Platform engine (T-07): table registry, generic records, conditions, custom tables, business rules, SLAs, notifications module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class PlatformModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPlatformModule(this IServiceCollection services)
    {
        return services;
    }
}
