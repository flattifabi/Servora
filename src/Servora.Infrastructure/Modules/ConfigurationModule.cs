using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Configuration;
using Servora.Application.Notifications;
using Servora.Infrastructure.Configuration;

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
        // System settings, feature flags and branding.
        services.AddSingleton<ISettingDefinitionProvider, CoreSettingDefinitionProvider>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IFeatureFlags, FeatureFlags>();
        services.AddScoped<IBrandingService, BrandingService>();

        // Secrets and database administration.
        services.AddScoped<ISecretAdminService, SecretAdminService>();
        services.AddScoped<IDatabaseAdminService, DatabaseAdminService>();

        // Setup wizard (works without a configured database).
        services.AddSingleton<SetupTokenStore>();
        services.AddSingleton<ISetupService, SetupService>();
        services.AddHostedService<SetupTokenInitializer>();

        // E-mail: real IEmailSender (replaces the kernel placeholder), outbox and SMTP transport.
        services.AddSingleton<ISmtpTransport, SmtpTransport>();
        services.AddSingleton<MailOutboxSignal>();
        services.AddScoped<SmtpOptionsProvider>();
        services.AddScoped<IEmailConfigurationService, EmailConfigurationService>();
        services.AddScoped<IEmailSender, QueuedEmailSender>();
        services.AddHostedService<MailOutboxProcessor>();
        return services;
    }
}
