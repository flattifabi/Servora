using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Servora.Application.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// The list of backups: the <c>*.svbak</c> files of the destination folder (and of the default folder, where
/// pre-migration backups fall back to when the destination is unreachable). Manifests are cached per file
/// (path + size + timestamp); verification results are remembered for the lifetime of the process.
/// </summary>
/// <param name="settings">The settings store.</param>
/// <param name="logger">The logger.</param>
public sealed class BackupCatalog(BackupSettingsStore settings, ILogger<BackupCatalog> logger)
{
    private readonly ConcurrentDictionary<string, BackupCatalogEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (long Size, DateTime Time, bool Valid)> _verified = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the folders that are scanned.</summary>
    /// <returns>The folders.</returns>
    public IReadOnlyList<string> GetFolders()
    {
        var destination = settings.ResolveDestination(settings.GetSchedule());
        return new[] { destination, settings.DefaultDestination }.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Lists all archives, newest first.</summary>
    /// <returns>The archives.</returns>
    public IReadOnlyList<BackupCatalogEntry> List()
    {
        var result = new List<BackupCatalogEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in GetFolders())
        {
            string[] files;
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                files = Directory.GetFiles(folder, "*" + BackupConstants.FileExtension, SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Backup folder {Folder} cannot be read", folder);
                continue;
            }

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (BackupFileNames.IsValidId(name) && seen.Add(name))
                {
                    result.Add(Describe(file));
                }
            }
        }

        foreach (var stale in _cache.Keys.Where(k => !result.Any(r => string.Equals(r.Path, k, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            _cache.TryRemove(stale, out _);
        }

        return result.OrderByDescending(e => e.CreatedAt).ToList();
    }

    /// <summary>Finds an archive by identifier.</summary>
    /// <param name="id">The identifier (file name).</param>
    /// <returns>The archive, or <see langword="null"/> when not found or the identifier is invalid.</returns>
    public BackupCatalogEntry? Find(string? id)
    {
        if (!BackupFileNames.IsValidId(id))
        {
            return null;
        }

        foreach (var folder in GetFolders())
        {
            var path = Path.Combine(folder, id!);
            if (File.Exists(path))
            {
                return Describe(path);
            }
        }

        return null;
    }

    /// <summary>Reads (or returns the cached) description of an archive file.</summary>
    /// <param name="path">The archive path.</param>
    /// <returns>The entry.</returns>
    public BackupCatalogEntry Describe(string path)
    {
        var info = new FileInfo(path);
        var integrity = _verified.TryGetValue(path, out var verified) && verified.Size == info.Length && verified.Time == info.LastWriteTimeUtc
            ? (verified.Valid ? BackupIntegrity.Valid : BackupIntegrity.Invalid)
            : BackupIntegrity.Unverified;

        if (_cache.TryGetValue(path, out var cached) && cached.SizeBytes == info.Length && cached.LastWriteUtc == info.LastWriteTimeUtc)
        {
            return cached.Manifest is null ? cached : cached with { Integrity = integrity };
        }

        BackupCatalogEntry entry;
        try
        {
            using var reader = BackupArchiveReader.Open(path);
            entry = new BackupCatalogEntry(info.Name, path, info.Length, info.LastWriteTimeUtc, reader.Manifest, integrity, null);
        }
        catch (Exception ex) when (ex is BackupException or IOException or UnauthorizedAccessException)
        {
            entry = new BackupCatalogEntry(info.Name, path, info.Length, info.LastWriteTimeUtc, null, BackupIntegrity.Invalid, ex.Message);
        }

        _cache[path] = entry;
        return entry;
    }

    /// <summary>Remembers the result of a verification.</summary>
    /// <param name="path">The archive path.</param>
    /// <param name="valid">Whether the archive is valid.</param>
    public void RecordVerification(string path, bool valid)
    {
        var info = new FileInfo(path);
        if (info.Exists)
        {
            _verified[path] = (info.Length, info.LastWriteTimeUtc, valid);
        }
    }

    /// <summary>Forgets an archive (after deletion).</summary>
    /// <param name="path">The archive path.</param>
    public void Forget(string path)
    {
        _cache.TryRemove(path, out _);
        _verified.TryRemove(path, out _);
    }
}
