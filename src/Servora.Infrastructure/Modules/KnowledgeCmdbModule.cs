using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Knowledge Management, CMDB and Asset Management (T-06) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class KnowledgeCmdbModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKnowledgeCmdbModule(this IServiceCollection services)
    {
        return services;
    }
}
