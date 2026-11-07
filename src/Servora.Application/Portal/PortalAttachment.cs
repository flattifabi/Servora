namespace Servora.Application.Portal;

/// <summary>A file attached to a portal item.</summary>
/// <param name="Id">The attachment identifier.</param>
/// <param name="FileName">The file name.</param>
/// <param name="SizeBytes">The size in bytes.</param>
/// <param name="UploadedBy">The uploader display name.</param>
/// <param name="UploadedAt">The UTC upload time.</param>
public sealed record PortalAttachment(Guid Id, string FileName, long SizeBytes, string UploadedBy, DateTimeOffset UploadedAt)
{
    /// <summary>Gets the relative download URL (ownership is checked by the endpoint).</summary>
    public string DownloadUrl => $"/portal/attachments/{Id}";
}
