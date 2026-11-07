namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-11 Self-Service Portal module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class PortalWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddPortalWeb(this WebApplicationBuilder builder)
    {
        return builder;
    }
}
