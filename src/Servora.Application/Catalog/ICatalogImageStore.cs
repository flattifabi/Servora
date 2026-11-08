namespace Servora.Application.Catalog;

/// <summary>
/// Stores catalog item images in the data directory, addressed by identifier. Callers validate the content (type
/// sniffing, size) and check permissions; the store only persists bytes.
/// </summary>
public interface ICatalogImageStore
{
    /// <summary>The maximum image size in bytes (2 MB).</summary>
    const int MaxImageBytes = 2 * 1024 * 1024;

    /// <summary>The relative URL prefix the images are served at (followed by the image identifier).</summary>
    const string UrlPrefix = "/portal/catalog-images/";

    /// <summary>Stores an image.</summary>
    /// <param name="content">The validated image bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new image identifier.</returns>
    Task<Guid> SaveAsync(byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Opens an image for reading.</summary>
    /// <param name="imageId">The image identifier.</param>
    /// <returns>The stream, or <see langword="null"/> when the image does not exist.</returns>
    Stream? Open(Guid imageId);

    /// <summary>Deletes an image (no-op when it does not exist).</summary>
    /// <param name="imageId">The image identifier.</param>
    void Delete(Guid imageId);

    /// <summary>Gets the relative URL of an image.</summary>
    /// <param name="imageId">The image identifier.</param>
    /// <returns>The URL, or <see langword="null"/> when no image is set.</returns>
    static string? UrlOf(Guid? imageId) => imageId is { } id ? UrlPrefix + id.ToString("N") : null;
}
