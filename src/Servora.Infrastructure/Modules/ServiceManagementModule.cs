using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Change Management, Service Catalog, Requests and Approvals (T-05) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ServiceManagementModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServiceManagementModule(this IServiceCollection services)
    {
        return services;
    }
}
