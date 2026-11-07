namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-07 Platform Engine module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class PlatformWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddPlatformWeb(this WebApplicationBuilder builder)
    {
        return builder;
    }
}
