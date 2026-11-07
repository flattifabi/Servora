using System.Globalization;
using System.Text.RegularExpressions;
using Servora.Application.Backup;
using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>Naming of archive files: <c>servora-yyyyMMdd-HHmmss-&lt;kind&gt;[-n].svbak</c>. The file name is the backup identifier.</summary>
public static partial class BackupFileNames
{
    /// <summary>The suffix that marks uploaded archives.</summary>
    public const string UploadedSuffix = "uploaded";

    /// <summary>Checks whether a backup identifier is a plain archive file name (no directories, no traversal).</summary>
    /// <param name="id">The identifier.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>Creates a unique archive path in a folder.</summary>
    /// <param name="folder">The destination folder.</param>
    /// <param name="createdAt">The creation time (UTC).</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The full path of a file that does not exist yet.</returns>
    public static string CreatePath(string folder, DateTimeOffset createdAt, BackupKind kind)
    {
        var stem = $"servora-{createdAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{KindSuffix(kind)}";
        var path = Path.Combine(folder, stem + BackupConstants.FileExtension);
        for (var n = 2; File.Exists(path) || File.Exists(path + ".partial"); n++)
        {
            path = Path.Combine(folder, $"{stem}-{n}{BackupConstants.FileExtension}");
        }

        return path;
    }

    /// <summary>Returns the effective kind of an archive file: uploaded archives keep the original kind in their manifest.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="manifestKind">The kind recorded in the manifest.</param>
    /// <returns>The effective kind.</returns>
    public static BackupKind EffectiveKind(string fileName, BackupKind manifestKind) =>
        UploadedPattern().IsMatch(fileName) ? BackupKind.Uploaded : manifestKind;

    private static string KindSuffix(BackupKind kind) => kind switch
    {
        BackupKind.Scheduled => "scheduled",
        BackupKind.Manual => "manual",
        BackupKind.PreMigration => "premigration",
        BackupKind.PreRestore => "prerestore",
        BackupKind.Uploaded => UploadedSuffix,
        _ => "backup",
    };

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,150}\.svbak$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"-uploaded(-\d+)?\.svbak$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UploadedPattern();
}
