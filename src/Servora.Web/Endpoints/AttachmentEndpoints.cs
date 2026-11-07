using Servora.Application.Activity;

namespace Servora.Web.Endpoints;

/// <summary>Download endpoint for record attachments (uploads happen through Blazor <c>InputFile</c>).</summary>
public static class AttachmentEndpoints
{
    /// <summary>Maps the endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/attachments/{id:guid}", DownloadAsync).RequireAuthorization().ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> DownloadAsync(Guid id, IAttachmentService attachments, CancellationToken cancellationToken)
    {
        var result = await attachments.OpenAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return Results.NotFound();
        }

        var (attachment, stream) = result.Value;

        // Always download (never render inline) to prevent stored XSS through uploaded HTML / SVG files.
        return Results.File(stream, "application/octet-stream", attachment.FileName, enableRangeProcessing: true);
    }
}
