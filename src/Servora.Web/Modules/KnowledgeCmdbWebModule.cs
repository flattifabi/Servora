namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-06 Knowledge, CMDB &amp; Assets module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class KnowledgeCmdbWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddKnowledgeCmdbWeb(this WebApplicationBuilder builder)
    {
        return builder;
    }
}
