using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Identity;
using Servora.Application.Identity.Admin;
using Servora.Application.Identity.Ldap;
using Servora.Application.Identity.Policies;
using Servora.Application.Identity.Profile;
using Servora.Application.Identity.Windows;
using Servora.Infrastructure.Identity;

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
        // Settings & policies
        services.AddScoped<IdentitySettingsStore>();
        services.AddScoped<IAuthPolicyProvider, AuthPolicyProvider>();
        services.AddScoped<IAuthPolicyAdminService, AuthPolicyAdminService>();

        // Administration
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IGroupAdminService, GroupAdminService>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<IProfileService, ProfileService>();

        // LDAP / Active Directory and Windows sign-in
        services.AddSingleton<IDirectoryClient, LdapDirectoryClient>();
        services.AddScoped<IDirectoryConfigurationProvider, DirectoryConfigurationProvider>();
        services.AddScoped<IDirectoryProvisioningService, DirectoryProvisioningService>();
        services.AddScoped<IDirectorySyncService, DirectorySyncService>();
        services.AddScoped<IDirectoryAdminService, DirectoryAdminService>();
        services.AddScoped<SystemScopeProvisioner>();
        services.AddScoped<IExternalCredentialValidator, LdapCredentialValidator>();
        services.AddScoped<IWindowsSignInService, WindowsSignInService>();
        services.AddHostedService<DirectorySyncHostedService>();

        // Demo data
        services.AddScoped<IDataSeeder, IdentityDemoSeeder>();
        return services;
    }
}
