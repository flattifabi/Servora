namespace Servora.Web.Modules;

/// <summary>Applies the host-level configuration of every module.</summary>
public static class WebModuleRegistration
{
    /// <summary>Applies all module host registrations.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddServoraWebModules(this WebApplicationBuilder builder)
    {
        builder.AddIdentityWeb();
        builder.AddConfigurationWeb();
        builder.AddItsmWeb();
        builder.AddServiceManagementWeb();
        builder.AddKnowledgeCmdbWeb();
        builder.AddPlatformWeb();
        builder.AddAutomationWeb();
        builder.AddObservabilityWeb();
        builder.AddApiMcpAiWeb();
        builder.AddPortalWeb();
        builder.AddBackupWeb();
        return builder;
    }
}
