namespace Servora.Web.Endpoints;

/// <summary>Maps the endpoints of every module.</summary>
public static class EndpointRegistration
{
    /// <summary>Maps all Servora HTTP endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapServoraEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapAttachmentEndpoints();
        app.MapIdentityEndpoints();
        app.MapConfigurationEndpoints();
        app.MapItsmEndpoints();
        app.MapServiceManagementEndpoints();
        app.MapKnowledgeCmdbEndpoints();
        app.MapPlatformEndpoints();
        app.MapAutomationEndpoints();
        app.MapObservabilityEndpoints();
        app.MapApiMcpAiEndpoints();
        app.MapPortalEndpoints();
        return app;
    }
}
