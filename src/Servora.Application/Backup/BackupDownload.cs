namespace Servora.Application.Backup;

/// <summary>An opened backup archive for download. The caller disposes <see cref="Content"/>.</summary>
/// <param name="FileName">The download file name.</param>
/// <param name="Content">The archive stream.</param>
/// <param name="Length">The archive length in bytes.</param>
public sealed record BackupDownload(string FileName, Stream Content, long Length);
