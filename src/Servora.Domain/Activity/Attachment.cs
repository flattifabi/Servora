using Servora.Domain.Common;

namespace Servora.Domain.Activity;

/// <summary>
/// File attached to a record. The binary content is stored on disk in the data directory (never inside the
/// database) under a random storage key so that user-supplied file names can never cause path traversal.
/// </summary>
public class Attachment : Entity
{
    /// <summary>Gets or sets the technical table name of the record.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the record identifier.</summary>
    public Guid RecordId { get; set; }

    /// <summary>Gets or sets the original (sanitized) file name for display and download.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the MIME content type.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Gets or sets the size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the random storage key (file name on disk).</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the SHA-256 hash of the content (hex).</summary>
    public string? Sha256 { get; set; }

    /// <summary>Gets or sets the uploader display name.</summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC upload time.</summary>
    public DateTimeOffset UploadedAt { get; set; }
}
