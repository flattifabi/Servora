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
        return app;
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
