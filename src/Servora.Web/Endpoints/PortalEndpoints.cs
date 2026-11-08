using Servora.Application.Catalog;
using Servora.Application.Portal;

namespace Servora.Web.Endpoints;

/// <summary>HTTP endpoints of the T-11 Self-Service Portal module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class PortalEndpoints
{
    /// <summary>Maps the module endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/portal/attachments/{id:guid}", DownloadAttachmentAsync).RequireAuthorization().ExcludeFromDescription();
        app.MapGet(ICatalogImageStore.UrlPrefix + "{id:guid}", CatalogImageAsync).RequireAuthorization().ExcludeFromDescription();
        app.MapGet(IPortalSettingsService.HeroImagePath, HeroImageAsync).RequireAuthorization().ExcludeFromDescription();
        return app;
    }

    /// <summary>
    /// Serves a catalog item image. Permissions (catalog browsing) and the item state are checked by
    /// <see cref="ICatalogOrderService"/>; the content type comes from the file content (raster formats only), and the
    /// response is marked <c>nosniff</c>.
    /// </summary>
    /// <param name="id">The image identifier.</param>
    /// <param name="catalog">The catalog ordering service.</param>
    /// <param name="context">The HTTP context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image, or 404.</returns>
    private static async Task<IResult> CatalogImageAsync(Guid id, ICatalogOrderService catalog, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await catalog.OpenImageAsync(id, cancellationToken);
        if (!result.Succeeded || result.Value is not { } image)
        {
            return Results.NotFound();
        }

        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "private, max-age=86400";
        return Results.Stream(image.Content, image.ContentType);
    }

    /// <summary>Serves the hero image of the portal home page (<c>portal.access</c> or <c>portal.manage</c>).</summary>
    /// <param name="settings">The portal settings service.</param>
    /// <param name="context">The HTTP context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image, or 404.</returns>
    private static async Task<IResult> HeroImageAsync(IPortalSettingsService settings, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await settings.OpenHeroImageAsync(cancellationToken);
        if (!result.Succeeded || result.Value is not { } image)
        {
            return Results.NotFound();
        }

        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "private, max-age=86400";
        return Results.Stream(image.Content, image.ContentType);
    }

    /// <summary>
    /// Downloads an attachment of one of the current user's own portal items. Ownership and <c>portal.access</c> are
    /// verified by <see cref="IPortalConversationService"/>; other users' files are reported as not found.
    /// </summary>
    /// <param name="id">The attachment identifier.</param>
    /// <param name="conversations">The portal conversation service.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file, or 404.</returns>
    private static async Task<IResult> DownloadAttachmentAsync(Guid id, IPortalConversationService conversations, CancellationToken cancellationToken)
    {
        var result = await conversations.OpenAttachmentAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return Results.NotFound();
        }

        var (fileName, stream) = result.Value;

        // Always download (never render inline) to prevent stored XSS through uploaded HTML / SVG files.
        return Results.File(stream, "application/octet-stream", fileName, enableRangeProcessing: true);
    }
}
