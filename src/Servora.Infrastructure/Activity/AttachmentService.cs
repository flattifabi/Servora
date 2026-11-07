using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Activity;
using Servora.Application.Common;
using Servora.Domain.Activity;

namespace Servora.Infrastructure.Activity;

/// <summary>
/// File-system backed <see cref="IAttachmentService"/>. Binaries are written to the attachment directory under a
/// random storage key; the user-supplied file name is only metadata and never used as a path.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="paths">The application paths.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="clock">The clock.</param>
public sealed class AttachmentService(IAppDbContextFactory dbFactory, IAppPaths paths, ICurrentUser currentUser, IClock clock) : IAttachmentService
{
    private static readonly char[] s_invalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Attachment>> ListAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Set<Attachment>().AsNoTracking().Where(a => a.TableName == tableName && a.RecordId == recordId).ToListAsync(cancellationToken);
        return items.OrderByDescending(a => a.UploadedAt).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<Attachment>> UploadAsync(string tableName, Guid recordId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        var safeName = Sanitize(fileName);
        if (safeName.Length == 0)
        {
            return Result<Attachment>.Invalid("The file name is invalid.");
        }

        var storageKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var path = Path.Combine(paths.AttachmentDirectory, storageKey);
        long size = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                size += read;
                if (size > IAttachmentService.MaxSizeBytes)
                {
                    await target.DisposeAsync();
                    File.Delete(path);
                    return Result<Attachment>.Invalid($"Attachments must not exceed {IAttachmentService.MaxSizeBytes / (1024 * 1024)} MB.");
                }

                sha.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }

        var attachment = new Attachment
        {
            TableName = tableName,
            RecordId = recordId,
            FileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType[..Math.Min(contentType.Length, 255)],
            SizeBytes = size,
            StorageKey = storageKey,
            Sha256 = Convert.ToHexStringLower(sha.GetHashAndReset()),
            UploadedBy = currentUser.DisplayName,
            UploadedAt = clock.UtcNow,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Set<Attachment>().Add(attachment);
        await db.SaveChangesAsync(cancellationToken);
        return Result<Attachment>.Success(attachment);
    }

    /// <inheritdoc />
    public async Task<Result<(Attachment Attachment, Stream Content)>> OpenAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await db.Set<Attachment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Result<(Attachment, Stream)>.NotFound();
        }

        var path = Path.Combine(paths.AttachmentDirectory, attachment.StorageKey);
        if (!File.Exists(path))
        {
            return Result<(Attachment, Stream)>.NotFound("The attachment content is missing.");
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Result<(Attachment, Stream)>.Success((attachment, stream));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return Result.NotFound();
        }

        db.Set<Attachment>().Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
        var path = Path.Combine(paths.AttachmentDirectory, attachment.StorageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Result.Success();
    }

    private static string Sanitize(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        name = string.Concat(name.Select(c => s_invalidFileNameChars.Contains(c) ? '_' : c));
        return name.Length > 255 ? name[..255] : name;
    }
}
