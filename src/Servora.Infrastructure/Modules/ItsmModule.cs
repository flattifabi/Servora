using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Incident &amp; Problem Management (T-04) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ItsmModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddItsmModule(this IServiceCollection services)
    {
        return services;
    }
}
