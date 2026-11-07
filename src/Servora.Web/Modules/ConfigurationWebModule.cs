using Microsoft.AspNetCore.Components.Server.Circuits;
using Servora.Web.Setup;

namespace Servora.Web.Modules;

/// <summary>Host-level registrations (authentication schemes, logging providers, options) of the T-03 Setup &amp; Configuration module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ConfigurationWebModule
{
    /// <summary>Applies the module's host configuration.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder.</returns>
    public static WebApplicationBuilder AddConfigurationWeb(this WebApplicationBuilder builder)
    {
        // Setup wizard: circuit-scoped access gate (loopback or setup token) and the handler that captures the client.
        builder.Services.AddScoped<SetupSession>();
        builder.Services.AddScoped<CircuitHandler, SetupCircuitHandler>();
        return builder;
    }
}
