using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Backup;
using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Disaster-recovery command line, handled by <c>Program.cs</c> before the web host runs:
/// <c>Servora backup list | create | verify &lt;file&gt; | restore &lt;file&gt;</c> with <c>--data-dir</c>,
/// <c>--password</c>, <c>--no-attachments</c>, <c>--no-safety-backup</c> and <c>--yes</c>. A restore runs immediately
/// in the command-line process, so the Servora service must be stopped first.
/// </summary>
public static class BackupCommandLine
{
    /// <summary>Exit code for success.</summary>
    public const int ExitSuccess = 0;

    /// <summary>Exit code for a failed operation.</summary>
    public const int ExitFailure = 1;

    /// <summary>Exit code for invalid arguments.</summary>
    public const int ExitUsage = 2;

    /// <summary>The environment variable that supplies the archive password without exposing it on the command line.</summary>
    public const string PasswordVariable = "SERVORA_BACKUP_PASSWORD";

    /// <summary>Gets the usage text.</summary>
    public static string HelpText { get; } =
        """
        Servora backup - disaster recovery command line

        Usage:
          Servora backup list                     List the backups of the backup folder.
          Servora backup create [--no-attachments] [--password <pw>]
                                                  Create a manual backup now.
          Servora backup verify <file> [--password <pw>]
                                                  Check the SHA-256 hashes (and signature) of a backup.
          Servora backup restore <file> [--password <pw>] [--no-safety-backup] [--yes]
                                                  Restore a backup immediately. Stop the Servora service first.

        Options:
          --data-dir <path>    Data directory (default: SERVORA_DATA_DIR, %ProgramData%\Servora for the service,
                               ./App_Data otherwise). Run from an elevated prompt for %ProgramData%\Servora.
          --password <pw>      Archive password (or set SERVORA_BACKUP_PASSWORD). Defaults to the configured password.
          --no-safety-backup   Do not create a pre-restore safety backup (for example when the database is broken).
          --yes                Do not ask for confirmation.

        <file> is a backup file name from 'backup list' or a path to a .svbak file.
        """;

    /// <summary>Checks whether the arguments start a backup command.</summary>
    /// <param name="args">The process arguments.</param>
    /// <returns><see langword="true"/> for <c>backup …</c>.</returns>
    public static bool IsBackupCommand(IReadOnlyList<string> args) => args.Count > 0 && string.Equals(args[0], "backup", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parses the arguments of a backup command.</summary>
    /// <param name="args">The process arguments (starting with <c>backup</c>).</param>
    /// <returns>The options.</returns>
    public static BackupCommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var options = new BackupCommandLineOptions { Password = Environment.GetEnvironmentVariable(PasswordVariable) };
        var positional = new List<string>();
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--data-dir":
                    options.DataDirectory = NextValue(args, ref i, arg, options);
                    break;
                case "--password":
                    options.Password = NextValue(args, ref i, arg, options);
                    break;
                case "--no-safety-backup":
                    options.NoSafetyBackup = true;
                    break;
                case "--no-attachments":
                    options.NoAttachments = true;
                    break;
                case "--yes" or "-y":
                    options.AssumeYes = true;
                    break;
                case "--help" or "-h" or "-?" or "/?":
                    options.Command = "help";
                    return options;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        options.Errors.Add($"Unknown option '{arg}'.");
                    }
                    else
                    {
                        positional.Add(arg);
                    }

                    break;
            }
        }

        options.Command = positional.Count > 0 ? positional[0].ToLowerInvariant() : "help";
        switch (options.Command)
        {
            case "list" or "create" or "help":
                if (positional.Count > 1)
                {
                    options.Errors.Add($"'{options.Command}' takes no file argument.");
                }

                break;
            case "verify" or "restore":
                if (positional.Count != 2)
                {
                    options.Errors.Add($"'{options.Command}' needs exactly one backup file.");
                }
                else
                {
                    options.File = positional[1];
                }

                break;
            default:
                options.Errors.Add($"Unknown command '{options.Command}'.");
                break;
        }

        return options;
    }

    /// <summary>Executes a parsed command.</summary>
    /// <param name="services">The application services.</param>
    /// <param name="options">The options.</param>
    /// <param name="input">The console input (confirmation).</param>
    /// <param name="output">The console output.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, BackupCommandLineOptions options, TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        if (options.Errors.Count > 0)
        {
            foreach (var error in options.Errors)
            {
                await output.WriteLineAsync("error: " + error);
            }

            await output.WriteLineAsync();
            await output.WriteLineAsync(HelpText);
            return ExitUsage;
        }

        if (options.Command == "help")
        {
            await output.WriteLineAsync(HelpText);
            return ExitSuccess;
        }

        var settings = services.GetRequiredService<BackupSettingsStore>();
        await output.WriteLineAsync($"Data directory: {settings.DataDirectory}");
        try
        {
            return options.Command switch
            {
                "list" => await ListAsync(services, output),
                "create" => await CreateAsync(services, options, output, cancellationToken),
                "verify" => await VerifyAsync(services, options, output, cancellationToken),
                "restore" => await RestoreAsync(services, options, input, output, cancellationToken),
                _ => ExitUsage,
            };
        }
        catch (BackupException ex)
        {
            await output.WriteLineAsync("error: " + ex.Message);
            return ExitFailure;
        }
    }

    private static string? NextValue(IReadOnlyList<string> args, ref int index, string option, BackupCommandLineOptions options)
    {
        if (index + 1 >= args.Count)
        {
            options.Errors.Add($"Option '{option}' needs a value.");
            return null;
        }

        index++;
        return args[index];
    }

    private static async Task<int> ListAsync(IServiceProvider services, TextWriter output)
    {
        var catalog = services.GetRequiredService<BackupCatalog>();
        var entries = catalog.List();
        await output.WriteLineAsync($"Backup folders: {string.Join(", ", catalog.GetFolders())}");
        if (entries.Count == 0)
        {
            await output.WriteLineAsync("No backups found.");
            return ExitSuccess;
        }

        await output.WriteLineAsync();
        await output.WriteLineAsync($"{"File",-48} {"Kind",-13} {"Created (local)",-19} {"Version",-10} {"Size",10}  Migration");
        foreach (var entry in entries)
        {
            var info = entry.ToInfo();
            var created = info.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var flags = (info.IsEncrypted ? " [encrypted]" : string.Empty) + (entry.Manifest is null ? " [unreadable]" : string.Empty);
            await output.WriteLineAsync($"{info.Id,-48} {info.Kind,-13} {created,-19} {info.AppVersion,-10} {FormatSize(info.SizeBytes),10}  {info.Migration ?? "-"}{flags}");
        }

        return ExitSuccess;
    }

    private static async Task<int> CreateAsync(IServiceProvider services, BackupCommandLineOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        if (!await EnsureDatabaseAsync(services, output))
        {
            return ExitFailure;
        }

        var jobs = services.GetRequiredService<BackupJobRunner>();
        await output.WriteLineAsync("Creating backup ...");
        var result = await jobs.RunAsync(BackupKind.Manual, $"Command line ({Environment.UserName})", cancellationToken, options.Password, options.NoAttachments ? false : null);
        AddPendingAudit(services, "backup.created", result.Entry.Id, "Manual backup created on the command line.");
        await output.WriteLineAsync($"Created {result.Entry.Path} ({FormatSize(result.Entry.SizeBytes)}, {result.Entry.Manifest?.Tables.Count} tables, {result.Entry.Manifest?.TotalRows:N0} rows{(result.Password is null ? string.Empty : ", encrypted")}).");
        return ExitSuccess;
    }

    private static async Task<int> VerifyAsync(IServiceProvider services, BackupCommandLineOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        var path = ResolveArchive(services, options.File!);
        var password = options.Password ?? await TryGetConfiguredPasswordAsync(services);
        var result = await services.GetRequiredService<BackupEngine>().VerifyAsync(path, password, cancellationToken);
        await output.WriteLineAsync($"Checked {result.EntriesChecked} entries ({FormatSize(result.BytesChecked)}){(result.SignatureChecked ? ", signature verified" : string.Empty)}.");
        foreach (var problem in result.Problems)
        {
            await output.WriteLineAsync("  problem: " + problem);
        }

        await output.WriteLineAsync(result.IsValid ? "The backup is valid." : "The backup is INVALID.");
        return result.IsValid ? ExitSuccess : ExitFailure;
    }

    private static async Task<int> RestoreAsync(IServiceProvider services, BackupCommandLineOptions options, TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        if (!await EnsureDatabaseAsync(services, output))
        {
            return ExitFailure;
        }

        var path = ResolveArchive(services, options.File!);
        var engine = services.GetRequiredService<BackupEngine>();
        var password = options.Password ?? await TryGetConfiguredPasswordAsync(services);
        using (var reader = BackupArchiveReader.Open(path))
        {
            if (reader.IsEncrypted)
            {
                reader.Unlock(password);
            }
            else
            {
                password = null;
            }

            var manifest = reader.Manifest;
            var problems = engine.CheckCompatibility(manifest);
            if (problems.Count > 0)
            {
                throw new BackupException(string.Join(" ", problems));
            }

            await output.WriteLineAsync($"Backup:    {Path.GetFileName(path)} ({manifest.Kind}, created {manifest.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} by {manifest.CreatedBy})");
            await output.WriteLineAsync($"Version:   {manifest.AppVersion}, migration {manifest.Migration ?? "-"}, {manifest.Tables.Count} tables, {manifest.TotalRows:N0} rows");
        }

        await output.WriteLineAsync("WARNING: all current data is replaced by the backup. Stop the Servora service before you continue.");
        if (!options.AssumeYes)
        {
            await output.WriteAsync($"Type {BackupConstants.RestoreConfirmationText} to continue: ");
            var answer = await input.ReadLineAsync(cancellationToken);
            if (!string.Equals(answer?.Trim(), BackupConstants.RestoreConfirmationText, StringComparison.Ordinal))
            {
                await output.WriteLineAsync("Restore cancelled.");
                return ExitFailure;
            }
        }

        string? safetyPath = null;
        string? safetyPassword = null;
        if (!options.NoSafetyBackup)
        {
            await output.WriteLineAsync("Creating the pre-restore safety backup ...");
            try
            {
                var safety = await services.GetRequiredService<BackupJobRunner>().RunAsync(BackupKind.PreRestore, $"Command line ({Environment.UserName}) before restoring {Path.GetFileName(path)}", cancellationToken);
                safetyPath = safety.Entry.Path;
                safetyPassword = safety.Password;
                await output.WriteLineAsync($"Safety backup: {safety.Entry.Path}");
            }
            catch (BackupException ex)
            {
                await output.WriteLineAsync($"error: the safety backup failed: {ex.Message}");
                await output.WriteLineAsync("Use --no-safety-backup to restore without it (for example when the current database is broken).");
                return ExitFailure;
            }
        }

        services.GetRequiredService<RestoreRequestStore>().Write(
            new RestoreRequest
            {
                BackupPath = path,
                BackupId = Path.GetFileName(path),
                SafetyBackupPath = safetyPath,
                RequestedAt = services.GetRequiredService<TimeProvider>().GetUtcNow(),
                RequestedBy = $"Command line ({Environment.UserName})",
            },
            password,
            safetyPassword);

        await output.WriteLineAsync("Restoring ...");
        var restored = await services.GetRequiredService<PendingRestoreProcessor>().RunAsync(cancellationToken);
        var status = services.GetRequiredService<BackupSettingsStore>().GetState().LastRestore;
        await output.WriteLineAsync(status?.Message ?? (restored ? "Restore completed." : "Restore failed."));
        if (restored)
        {
            await output.WriteLineAsync("Start the Servora service; it migrates the restored database to the current version automatically.");
        }

        return restored ? ExitSuccess : ExitFailure;
    }

    private static string ResolveArchive(IServiceProvider services, string file)
    {
        var entry = services.GetRequiredService<BackupCatalog>().Find(file);
        if (entry is not null)
        {
            return entry.Path;
        }

        var full = Path.GetFullPath(file);
        return File.Exists(full) ? full : throw new BackupException($"The backup '{file}' was found neither in the backup folder nor as a file.");
    }

    private static async Task<bool> EnsureDatabaseAsync(IServiceProvider services, TextWriter output)
    {
        if (services.GetRequiredService<IBackupDatabase>().IsConfigured)
        {
            return true;
        }

        await output.WriteLineAsync("error: no database is configured in this data directory (database.json). Run the setup wizard or pre-configure Servora:Database (see docs/operations/configuration.md).");
        return false;
    }

    private static async Task<string?> TryGetConfiguredPasswordAsync(IServiceProvider services)
    {
        try
        {
            return services.GetRequiredService<IBackupDatabase>().IsConfigured
                ? await services.GetRequiredService<IBackupPasswordSource>().GetPasswordAsync()
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static void AddPendingAudit(IServiceProvider services, string action, string backupId, string message)
    {
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        services.GetRequiredService<BackupSettingsStore>().UpdateState(s => s.PendingAudits.Add(new PendingBackupAudit
        {
            Action = action,
            BackupId = backupId,
            Message = message,
            Actor = $"Command line ({Environment.UserName})",
            At = now,
        }));
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / 1024d:0.0} KB",
        _ => $"{bytes} B",
    };
}
