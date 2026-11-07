using System.Text.RegularExpressions;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Structural validation of a manifest read from an archive. Archives can be uploaded, so every value that later
/// reaches the file system (entry paths) or SQL (identifiers, store types) is checked here.
/// </summary>
public static partial class BackupManifestValidator
{
    /// <summary>Checks whether an entry path is a safe, relative, forward-slash path without traversal segments.</summary>
    /// <param name="path">The entry path.</param>
    /// <returns><see langword="true"/> when safe.</returns>
    public static bool IsSafeEntryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 400 || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
        {
            return false;
        }

        if (path.Any(c => char.IsControl(c) || c is '<' or '>' or '"' or '|' or '?' or '*'))
        {
            return false;
        }

        return path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != ".." && segment.Trim() == segment);
    }

    /// <summary>Checks whether a store type is a plain type name that can safely be used in a SQL <c>CAST</c>.</summary>
    /// <param name="storeType">The store type.</param>
    /// <returns><see langword="true"/> when safe.</returns>
    public static bool IsSafeStoreType(string? storeType) => storeType is not null && StoreTypePattern().IsMatch(storeType);

    /// <summary>Validates a manifest.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <returns>User-safe problems; empty when the manifest is valid.</returns>
    public static IReadOnlyList<string> Validate(BackupManifest? manifest)
    {
        var problems = new List<string>();
        if (manifest is null)
        {
            problems.Add("The manifest is empty.");
            return problems;
        }

        if (manifest.Product != BackupArchiveFormat.Product)
        {
            problems.Add("The archive is not a Servora backup.");
        }

        if (manifest.FormatVersion < 1 || manifest.FormatVersion > BackupArchiveFormat.FormatVersion)
        {
            problems.Add($"The archive format version {manifest.FormatVersion} is not supported by this Servora version.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ProviderName) || string.IsNullOrWhiteSpace(manifest.Provider))
        {
            problems.Add("The archive does not name its database provider.");
        }

        if (manifest.Migration is { Length: > 300 })
        {
            problems.Add("The migration identifier is invalid.");
        }

        var entries = new Dictionary<string, BackupEntryManifest>(StringComparer.OrdinalIgnoreCase);
        var indexes = new HashSet<int>();
        foreach (var entry in manifest.Entries ?? [])
        {
            if (!IsSafeEntryPath(entry.Path) || entry.Path is BackupArchiveFormat.ManifestEntry or BackupArchiveFormat.SignatureEntry)
            {
                problems.Add($"The archive entry path '{entry.Path}' is not allowed.");
                continue;
            }

            if (!entries.TryAdd(entry.Path, entry) || !indexes.Add(entry.Index) || entry.Index < 0)
            {
                problems.Add($"The archive entry '{entry.Path}' is duplicated.");
            }

            if (entry.Length < 0 || entry.Sha256 is not { Length: 64 } || !entry.Sha256.All(Uri.IsHexDigit))
            {
                problems.Add($"The archive entry '{entry.Path}' has invalid integrity information.");
            }

            var expectedPrefix = entry.Kind switch
            {
                BackupArchiveFormat.KindTable => BackupArchiveFormat.DatabasePrefix,
                BackupArchiveFormat.KindKey => BackupArchiveFormat.KeysPrefix,
                BackupArchiveFormat.KindAttachment => BackupArchiveFormat.AttachmentsPrefix,
                BackupArchiveFormat.KindFile => BackupArchiveFormat.DataPrefix,
                _ => null,
            };
            if (expectedPrefix is null || !entry.Path.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                problems.Add($"The archive entry '{entry.Path}' has an unknown kind.");
            }
        }

        var tableKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in manifest.Tables ?? [])
        {
            if (!IsValidIdentifier(table.Name) || (table.Schema is not null && !IsValidIdentifier(table.Schema)))
            {
                problems.Add("A table name in the archive is invalid.");
                continue;
            }

            if (!tableKeys.Add(table.Key))
            {
                problems.Add($"The table '{table.Key}' is listed twice.");
            }

            if (!entries.TryGetValue(table.Entry ?? string.Empty, out var tableEntry) || tableEntry.Kind != BackupArchiveFormat.KindTable)
            {
                problems.Add($"The data of table '{table.Key}' is missing.");
            }

            if (table.RowCount < 0 || table.Columns is not { Count: > 0 })
            {
                problems.Add($"The table '{table.Key}' has no columns.");
                continue;
            }

            var columns = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in table.Columns)
            {
                if (!IsValidIdentifier(column.Name) || !columns.Add(column.Name))
                {
                    problems.Add($"A column name of table '{table.Key}' is invalid or duplicated.");
                }

                if (!IsSafeStoreType(column.StoreType))
                {
                    problems.Add($"The store type of column '{table.Key}.{column.Name}' is invalid.");
                }

                if (column.DbType is { } dbType && !Enum.IsDefined(dbType))
                {
                    problems.Add($"The parameter type of column '{table.Key}.{column.Name}' is invalid.");
                }
            }

            if ((table.PrimaryKey ?? []).Any(c => !columns.Contains(c)))
            {
                problems.Add($"The primary key of table '{table.Key}' references an unknown column.");
            }

            foreach (var foreignKey in table.ForeignKeys ?? [])
            {
                if (foreignKey.Columns is not { Count: > 0 } || foreignKey.Columns.Any(c => !columns.Contains(c)) ||
                    !IsValidIdentifier(foreignKey.PrincipalTable) || (foreignKey.PrincipalSchema is not null && !IsValidIdentifier(foreignKey.PrincipalSchema)))
                {
                    problems.Add($"A foreign key of table '{table.Key}' is invalid.");
                }
            }
        }

        return problems;
    }

    private static bool IsValidIdentifier(string? identifier) =>
        !string.IsNullOrWhiteSpace(identifier) && identifier.Length <= 128 && !identifier.Any(char.IsControl);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_ ]{0,62}(\(\s*(\d{1,5}|max)\s*(,\s*\d{1,5}\s*)?\))?( (with|without) time zone)?(\[\])?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StoreTypePattern();
}
