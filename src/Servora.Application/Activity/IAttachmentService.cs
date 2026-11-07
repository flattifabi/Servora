using Servora.Application.Common;
using Servora.Domain.Activity;

namespace Servora.Application.Activity;

/// <summary>Stores and retrieves files attached to records.</summary>
public interface IAttachmentService
{
    /// <summary>The maximum accepted attachment size in bytes (25 MB).</summary>
    const long MaxSizeBytes = 25L * 1024 * 1024;

    /// <summary>Lists the attachments of a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attachments, newest first.</returns>
    Task<IReadOnlyList<Attachment>> ListAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>Uploads a file.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="fileName">The original file name (sanitized by the service).</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="content">The content stream; read up to <see cref="MaxSizeBytes"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored attachment.</returns>
    Task<Result<Attachment>> UploadAsync(string tableName, Guid recordId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens an attachment for download.</summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The metadata and a read stream the caller must dispose.</returns>
    Task<Result<(Attachment Attachment, Stream Content)>> OpenAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>Deletes an attachment.</summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
