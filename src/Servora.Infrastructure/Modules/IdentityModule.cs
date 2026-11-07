using Microsoft.Extensions.DependencyInjection;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Identity &amp; Access (T-02): users, groups, roles, LDAP / Active Directory, Windows authentication, directory sync module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class IdentityModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        return services;
    }
}
