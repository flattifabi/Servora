using Microsoft.Net.Http.Headers;
using Servora.Application.Configuration;

namespace Servora.Web.Endpoints;

/// <summary>HTTP endpoints of the T-03 Setup &amp; Configuration module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ConfigurationEndpoints
{
    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        // The logo is public: it is shown on the sign-in page.
        app.MapGet(IBrandingService.LogoPath, GetLogoAsync).AllowAnonymous().ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> GetLogoAsync(HttpContext context, IBrandingService branding, CancellationToken cancellationToken)
    {
        var logo = await branding.OpenLogoAsync(cancellationToken);
        if (logo is null)
        {
            return Results.NotFound();
        }

        // The content type was verified from the magic bytes on upload (raster images only). The sandbox CSP makes
        // sure the response can never execute script even when opened directly.
        context.Response.Headers.CacheControl = "public, max-age=86400";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; sandbox";
        return Results.File(logo.Content, logo.ContentType, entityTag: new EntityTagHeaderValue($"\"{logo.Version}\""));
    }
}
