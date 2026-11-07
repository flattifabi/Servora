using Servora.Application.Common;

namespace Servora.Application.Portal;

/// <summary>
/// Public comments and attachments of the current user's own incidents and requests. Requires <c>portal.access</c>; the
/// record must belong to the user (caller / requested-for / opened by). Work notes and field changes are never returned
/// and cannot be written.
/// </summary>
public interface IPortalConversationService
{
    /// <summary>Gets the public conversation and attachments of one of the user's items.</summary>
    /// <param name="kind">The kind of item.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The conversation.</returns>
    Task<Result<PortalConversation>> GetAsync(PortalTicketKind kind, Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>Adds a public comment (visible to the fulfillers and the requester) to one of the user's open items.</summary>
    /// <param name="kind">The kind of item.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="text">The comment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created entry.</returns>
    Task<Result<PortalConversationEntry>> AddCommentAsync(PortalTicketKind kind, Guid recordId, string text, CancellationToken cancellationToken = default);

    /// <summary>Attaches a file to one of the user's open items.</summary>
    /// <param name="kind">The kind of item.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="content">The content (read up to the attachment size limit).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored attachment.</returns>
    Task<Result<PortalAttachment>> UploadAsync(PortalTicketKind kind, Guid recordId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens an attachment of one of the user's items for download.</summary>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file name and a read stream the caller must dispose.</returns>
    Task<Result<(string FileName, Stream Content)>> OpenAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
