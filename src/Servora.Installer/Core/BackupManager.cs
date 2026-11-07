using System.Globalization;
using System.Text.RegularExpressions;
using Servora.Installer.Abstractions;

namespace Servora.Installer.Core;

/// <summary>
/// Creates, restores and prunes binary backups of the install directory. Backups live in
/// <c>%ProgramData%\Servora\backups\&lt;yyyyMMdd-HHmmss&gt;[-v&lt;version&gt;]</c>, so their names sort chronologically.
/// </summary>
/// <param name="fileSystem">The file system.</param>
/// <param name="copier">The tree copier.</param>
/// <param name="time">The clock.</param>
internal sealed partial class BackupManager(IFileSystem fileSystem, FileTreeCopier copier, TimeProvider time)
{
    /// <summary>Copies the install directory into a new backup folder.</summary>
    /// <param name="installDirectory">The install directory.</param>
    /// <param name="backupRoot">The backup root directory.</param>
    /// <param name="version">The installed version (part of the folder name), or <see langword="null"/>.</param>
    /// <param name="excludedTopLevelNames">Top-level entries that are data, not binaries (never backed up).</param>
    /// <returns>The full path of the new backup.</returns>
    public string Create(string installDirectory, string backupRoot, string? version, IReadOnlySet<string> excludedTopLevelNames)
    {
        fileSystem.CreateDirectory(backupRoot);
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var suffix = string.IsNullOrWhiteSpace(version) || version == "unknown" ? string.Empty : "-v" + SanitizeVersion(version);
        var path = Path.Combine(backupRoot, stamp + suffix);
        for (var n = 2; fileSystem.DirectoryExists(path); n++)
        {
            path = Path.Combine(backupRoot, $"{stamp}{suffix}-{n}");
        }

        copier.Copy(installDirectory, path, excludedTopLevelNames);
        return path;
    }

    /// <summary>Replaces the content of the install directory with a backup.</summary>
    /// <param name="backupPath">The backup folder.</param>
    /// <param name="installDirectory">The install directory.</param>
    /// <param name="preservedTopLevelNames">Top-level entries of the install directory that must not be deleted.</param>
    public void Restore(string backupPath, string installDirectory, IReadOnlySet<string> preservedTopLevelNames)
    {
        copier.Clear(installDirectory, preservedTopLevelNames);
        copier.Copy(backupPath, installDirectory);
    }

    /// <summary>Lists the backups, newest first.</summary>
    /// <param name="backupRoot">The backup root directory.</param>
    /// <returns>The backup folder paths.</returns>
    public IReadOnlyList<string> List(string backupRoot)
    {
        if (!fileSystem.DirectoryExists(backupRoot))
        {
            return [];
        }

        return fileSystem.GetDirectories(backupRoot)
            .Where(d => BackupNamePattern().IsMatch(Path.GetFileName(d)))
            .OrderByDescending(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Deletes the oldest backups so that at most <paramref name="keep"/> remain.</summary>
    /// <param name="backupRoot">The backup root directory.</param>
    /// <param name="keep">The number of backups to keep (at least 1).</param>
    /// <param name="protectedPath">A backup that is never deleted (the one just created).</param>
    /// <returns>The deleted backup paths.</returns>
    public IReadOnlyList<string> Prune(string backupRoot, int keep, string? protectedPath)
    {
        keep = Math.Max(1, keep);
        var deleted = new List<string>();
        foreach (var backup in List(backupRoot).Skip(keep))
        {
            if (protectedPath is not null && string.Equals(backup, protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            copier.Delete(backup);
            deleted.Add(backup);
        }

        return deleted;
    }

    private static string SanitizeVersion(string version) =>
        new(ProductVersion.Display(version).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_').ToArray());

    [GeneratedRegex(@"^\d{8}-\d{6}", RegexOptions.CultureInvariant)]
    private static partial Regex BackupNamePattern();
}
