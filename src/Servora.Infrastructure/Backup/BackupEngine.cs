using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Servora.Application.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Provider-neutral backup engine: creates archives (logical dump of every table + data directory files), verifies
/// them and restores them (migrate to the backup's migration → replace all rows → restore files). It performs no
/// authorization and takes no locks – callers (<see cref="BackupService"/>, scheduler, startup, command line) do.
/// </summary>
/// <param name="database">The database access.</param>
/// <param name="settings">The settings store (data directory locations).</param>
/// <param name="catalog">The catalog.</param>
/// <param name="time">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed class BackupEngine(IBackupDatabase database, BackupSettingsStore settings, BackupCatalog catalog, TimeProvider time, ILogger<BackupEngine> logger)
{
    private static readonly string[] s_dataFolders = ["branding", "uploads"];

    /// <summary>Gets the Servora version written to manifests.</summary>
    public static string AppVersion { get; } =
        (typeof(BackupEngine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>Creates a backup archive.</summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The catalog entry of the new archive.</returns>
    public async Task<BackupCatalogEntry> CreateAsync(BackupCreateOptions options, CancellationToken cancellationToken)
    {
        var createdAt = time.GetUtcNow();
        Directory.CreateDirectory(options.Destination);
        var finalPath = BackupFileNames.CreatePath(options.Destination, createdAt, options.Kind);
        var partialPath = finalPath + ".partial";
        logger.LogInformation("Creating {Kind} backup {File}", options.Kind, Path.GetFileName(finalPath));

        try
        {
            await using var db = database.CreateContext();
            var creator = db.GetService<IRelationalDatabaseCreator>();
            if (!await creator.ExistsAsync(cancellationToken) || !await creator.HasTablesAsync(cancellationToken))
            {
                throw new BackupException("The database has no tables yet. Start Servora once so that it creates the schema, then create the backup.");
            }

            var schema = await BackupSchemaReader.ReadCurrentAsync(db, cancellationToken);
            using var cipher = string.IsNullOrEmpty(options.Password) ? null : BackupCipher.Create(options.Password, options.Iterations);
            var manifest = new BackupManifest
            {
                AppVersion = AppVersion,
                Provider = database.ProviderKey,
                ProviderName = db.Database.ProviderName ?? string.Empty,
                Migration = schema.Migration,
                CreatedAt = createdAt,
                CreatedBy = options.CreatedBy,
                MachineName = Environment.MachineName,
                Kind = options.Kind,
                IncludesAttachments = options.IncludeAttachments,
                IncludesInstanceSettings = options.IncludeInstanceSettings,
            };

            var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            using (var writer = new BackupArchiveWriter(output, cipher))
            {
                await DumpDatabaseAsync(db, schema, writer, manifest, cancellationToken);
                await WriteDataFilesAsync(writer, options, cancellationToken);
                await writer.CompleteAsync(manifest, cancellationToken);
            }

            File.Move(partialPath, finalPath);
            catalog.RecordVerification(finalPath, valid: true);
            var entry = catalog.Describe(finalPath);
            logger.LogInformation(
                "Backup {File} created: {Tables} tables, {Rows} rows, {Size:N0} bytes{Encrypted}",
                entry.Id, manifest.Tables.Count, manifest.TotalRows, entry.SizeBytes, cipher is null ? string.Empty : ", encrypted");
            return entry;
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    /// <summary>Verifies an archive (entry hashes; password and signature when a password is supplied for an encrypted archive).</summary>
    /// <param name="path">The archive path.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<BackupVerificationResult> VerifyAsync(string path, string? password, CancellationToken cancellationToken)
    {
        BackupVerificationResult result;
        try
        {
            using var reader = BackupArchiveReader.Open(path);
            var passwordProblem = default(string);
            if (reader.IsEncrypted && !string.IsNullOrEmpty(password))
            {
                try
                {
                    reader.Unlock(password);
                }
                catch (BackupPasswordException ex)
                {
                    passwordProblem = ex.Message;
                }
            }

            result = await reader.VerifyAsync(cancellationToken);
            if (passwordProblem is not null)
            {
                result = result with { IsValid = false, Problems = [.. result.Problems, passwordProblem] };
            }
        }
        catch (BackupException ex)
        {
            result = new BackupVerificationResult(false, 0, 0, false, [ex.Message]);
        }

        catalog.RecordVerification(path, result.IsValid);
        return result;
    }

    /// <summary>Checks whether an archive can be restored into the configured database.</summary>
    /// <param name="manifest">The archive manifest.</param>
    /// <returns>User-safe problems; empty when compatible.</returns>
    public IReadOnlyList<string> CheckCompatibility(BackupManifest manifest)
    {
        using var db = database.CreateContext();
        return CheckCompatibility(db, manifest);
    }

    /// <summary>
    /// Restores an archive immediately. Only call this while no other part of the application uses the database
    /// (startup before migrations, command line with the service stopped).
    /// </summary>
    /// <param name="path">The archive path.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<BackupRestoreResult> RestoreAsync(string path, BackupRestoreOptions options, CancellationToken cancellationToken)
    {
        using var reader = BackupArchiveReader.Open(path);
        reader.Unlock(options.Password);
        var verification = await reader.VerifyAsync(cancellationToken);
        if (!verification.IsValid)
        {
            throw new BackupIntegrityException("The backup failed verification: " + string.Join(" ", verification.Problems.Take(5)));
        }

        var manifest = reader.Manifest;
        await using (var db = database.CreateContext())
        {
            var problems = CheckCompatibility(db, manifest);
            if (problems.Count > 0)
            {
                throw new BackupException(string.Join(" ", problems));
            }

            options.DatabaseModified = true;
            if (manifest.Migration is not null)
            {
                logger.LogWarning("Restore: migrating the database to the backup's migration {Migration}", manifest.Migration);
                await db.GetService<IMigrator>().MigrateAsync(manifest.Migration, cancellationToken);
            }
            else
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
        }

        long rows;
        await using (var db = database.CreateContext())
        {
            var target = BackupSchemaReader.ReadForMigration(db, manifest.Migration);
            rows = await DatabaseRestorer.RestoreAsync(db, reader, target, logger, cancellationToken);
        }

        var files = options.RestoreFiles ? await RestoreFilesAsync(reader, cancellationToken) : 0;
        logger.LogWarning("Restore of {File} completed: {Tables} tables, {Rows} rows, {Files} files", Path.GetFileName(path), manifest.Tables.Count, rows, files);
        return new BackupRestoreResult(rows, manifest.Tables.Count, files, manifest.Migration);
    }

    private static IReadOnlyList<string> CheckCompatibility(DbContext db, BackupManifest manifest)
    {
        var problems = new List<string>();
        if (!string.Equals(manifest.ProviderName, db.Database.ProviderName, StringComparison.Ordinal))
        {
            problems.Add($"The backup was created on {manifest.Provider}, but this installation uses a different database provider. Restores are only possible into the same provider.");
            return problems;
        }

        var migrations = db.Database.GetMigrations().ToList();
        if (manifest.Migration is not null && !migrations.Contains(manifest.Migration, StringComparer.Ordinal))
        {
            problems.Add($"The backup is at database migration '{manifest.Migration}', which this Servora version ({AppVersion}) does not know. It was created by a newer version (backup version {manifest.AppVersion}); install that version or newer to restore it.");
        }
        else if (manifest.Migration is null && migrations.Count > 0)
        {
            problems.Add("The backup was created by a development build without database migrations and cannot be restored into this version.");
        }

        return problems;
    }

    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new BackupIntegrityException($"The archive entry '{relative}' points outside its target folder.");
        }

        return full;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover .partial file is ignored by the catalog.
        }
    }

    private async Task DumpDatabaseAsync(DbContext db, BackupSchemaSnapshot schema, BackupArchiveWriter writer, BackupManifest manifest, CancellationToken cancellationToken)
    {
        var sql = db.GetService<ISqlGenerationHelper>();
        var dialect = RestoreDialect.For(db.Database.ProviderName);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = db.Database.GetDbConnection();
            var transaction = await dialect.BeginBackupAsync(connection, cancellationToken);
            try
            {
                var index = 0;
                foreach (var table in schema.Tables)
                {
                    var entryPath = $"{BackupArchiveFormat.DatabasePrefix}{++index:D4}.bin";
                    long rows = 0;
                    await writer.WriteEntryAsync(
                        entryPath,
                        BackupArchiveFormat.KindTable,
                        async (stream, ct) => rows = await DatabaseDumper.DumpTableAsync(connection, transaction, sql, table, stream, ct),
                        cancellationToken);
                    manifest.Tables.Add(new BackupTableManifest
                    {
                        Name = table.Name,
                        Schema = table.Schema,
                        Entry = entryPath,
                        RowCount = rows,
                        Columns = table.Columns,
                        PrimaryKey = table.PrimaryKey,
                        ForeignKeys = table.ForeignKeys,
                    });
                }
            }
            finally
            {
                await dialect.EndBackupAsync(connection, transaction, CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task WriteDataFilesAsync(BackupArchiveWriter writer, BackupCreateOptions options, CancellationToken cancellationToken)
    {
        if (Directory.Exists(settings.KeyDirectory))
        {
            foreach (var file in Directory.GetFiles(settings.KeyDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                await WriteFileAsync(writer, BackupArchiveFormat.KeysPrefix + Path.GetFileName(file), BackupArchiveFormat.KindKey, file, cancellationToken);
            }
        }

        if (options.IncludeAttachments)
        {
            await WriteTreeAsync(writer, settings.AttachmentDirectory, BackupArchiveFormat.AttachmentsPrefix, BackupArchiveFormat.KindAttachment, cancellationToken);
        }

        foreach (var folder in s_dataFolders)
        {
            await WriteTreeAsync(writer, Path.Combine(settings.DataDirectory, folder), $"{BackupArchiveFormat.DataPrefix}{folder}/", BackupArchiveFormat.KindFile, cancellationToken);
        }

        var instanceSettings = Path.Combine(settings.DataDirectory, "appsettings.json");
        if (options.IncludeInstanceSettings && File.Exists(instanceSettings))
        {
            await WriteFileAsync(writer, BackupArchiveFormat.DataPrefix + "appsettings.json", BackupArchiveFormat.KindFile, instanceSettings, cancellationToken);
        }
    }

    private async Task WriteTreeAsync(BackupArchiveWriter writer, string root, string prefix, string kind, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            await WriteFileAsync(writer, prefix + relative, kind, file, cancellationToken);
        }
    }

    private async Task WriteFileAsync(BackupArchiveWriter writer, string entryPath, string kind, string file, CancellationToken cancellationToken)
    {
        if (!BackupManifestValidator.IsSafeEntryPath(entryPath))
        {
            logger.LogWarning("Skipping file {File}: its name cannot be stored in a backup archive", file);
            return;
        }

        await writer.WriteFileAsync(entryPath, kind, file, cancellationToken);
    }

    private async Task<int> RestoreFilesAsync(BackupArchiveReader reader, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var entry in reader.Manifest.Entries)
        {
            string? target = null;
            var overwrite = true;
            switch (entry.Kind)
            {
                case BackupArchiveFormat.KindKey:
                    var keyName = entry.Path[BackupArchiveFormat.KeysPrefix.Length..];
                    if (keyName.Contains('/'))
                    {
                        continue;
                    }

                    // Merge the key ring: never replace or remove current keys (database.json is protected with them).
                    target = SafeCombine(settings.KeyDirectory, keyName);
                    overwrite = false;
                    break;
                case BackupArchiveFormat.KindAttachment:
                    target = SafeCombine(settings.AttachmentDirectory, entry.Path[BackupArchiveFormat.AttachmentsPrefix.Length..]);
                    break;
                case BackupArchiveFormat.KindFile:
                    var relative = entry.Path[BackupArchiveFormat.DataPrefix.Length..];
                    if (relative == "appsettings.json")
                    {
                        // Never overwrite the live instance settings (ports, certificates) – keep a copy for the administrator.
                        target = Path.Combine(settings.DataDirectory, "appsettings.from-backup.json");
                    }
                    else if (s_dataFolders.Any(f => relative.StartsWith(f + "/", StringComparison.Ordinal)))
                    {
                        target = SafeCombine(settings.DataDirectory, relative);
                    }

                    break;
            }

            if (target is null || (!overwrite && File.Exists(target)))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp = target + ".restore-tmp";
            await using (var source = reader.OpenEntry(entry))
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await source.CopyToAsync(output, cancellationToken);
            }

            File.Move(temp, target, overwrite: true);
            count++;
        }

        return count;
    }
}
