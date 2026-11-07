using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Setup &amp; Configuration (T-03): setup wizard, system properties, branding, SMTP e-mail, secrets administration module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ConfigurationModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddConfigurationModule(this IServiceCollection services)
    {
        return services;
    }
}
