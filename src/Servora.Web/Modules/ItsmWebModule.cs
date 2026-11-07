namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-04 Incident &amp; Problem Management module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ItsmWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddItsmWeb(this WebApplicationBuilder builder)
    {
        return builder;
    }
}
