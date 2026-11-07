using System.Text.Json;
using Servora.Application.Common;
using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Reads and writes <c>&lt;data dir&gt;/backup.json</c> (schedule, retention and runtime state). The file lives outside
/// the database so it survives restores, and it is re-read on every access so the command line and the service see
/// each other's changes. Writes are atomic (temporary file + rename).
/// </summary>
public sealed class BackupSettingsStore
{
    private readonly object _sync = new();
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="BackupSettingsStore"/> class.</summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="time">The time provider.</param>
    public BackupSettingsStore(IAppPaths paths, TimeProvider time)
    {
        _time = time;
        DataDirectory = paths.DataDirectory;
        KeyDirectory = paths.KeyDirectory;
        AttachmentDirectory = paths.AttachmentDirectory;
        FilePath = Path.Combine(paths.DataDirectory, "backup.json");
        BackupRoot = Path.Combine(paths.DataDirectory, "backups");
        DefaultDestination = Path.Combine(BackupRoot, "instance");
        RestoreRequestPath = Path.Combine(paths.DataDirectory, "restore-request.json");
        LockFilePath = Path.Combine(BackupRoot, ".backup.lock");
    }

    /// <summary>Gets the data directory.</summary>
    public string DataDirectory { get; }

    /// <summary>Gets the Data Protection key directory.</summary>
    public string KeyDirectory { get; }

    /// <summary>Gets the attachment directory.</summary>
    public string AttachmentDirectory { get; }

    /// <summary>Gets the path of <c>backup.json</c>.</summary>
    public string FilePath { get; }

    /// <summary>Gets <c>&lt;data dir&gt;/backups</c> (shared with the installer's binary backups).</summary>
    public string BackupRoot { get; }

    /// <summary>Gets the default destination <c>&lt;data dir&gt;/backups/instance</c>.</summary>
    public string DefaultDestination { get; }

    /// <summary>Gets the path of the restore request file.</summary>
    public string RestoreRequestPath { get; }

    /// <summary>Gets the path of the cross-process operation lock file.</summary>
    public string LockFilePath { get; }

    /// <summary>Returns a copy of the schedule.</summary>
    /// <returns>The schedule.</returns>
    public BackupSchedule GetSchedule() => Load().Schedule;

    /// <summary>Returns a copy of the runtime state.</summary>
    /// <returns>The state.</returns>
    public BackupRunState GetState() => Load().State;

    /// <summary>Resolves the destination folder of a schedule.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <returns>The absolute destination folder.</returns>
    public string ResolveDestination(BackupSchedule schedule) =>
        string.IsNullOrWhiteSpace(schedule.DestinationFolder)
            ? DefaultDestination
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(schedule.DestinationFolder.Trim()), DataDirectory);

    /// <summary>Saves a new schedule. Resets the scheduling baseline so that no missed occurrence runs immediately.</summary>
    /// <param name="schedule">The schedule.</param>
    public void SaveSchedule(BackupSchedule schedule) => Update(document =>
    {
        document.Schedule = schedule.Clone();
        document.State.ScheduleBaseline = _time.GetUtcNow();
    });

    /// <summary>Changes the runtime state.</summary>
    /// <param name="change">The change.</param>
    public void UpdateState(Action<BackupRunState> change) => Update(document => change(document.State));

    private BackupSettingsDocument Load()
    {
        lock (_sync)
        {
            return LoadUnsafe();
        }
    }

    private void Update(Action<BackupSettingsDocument> change)
    {
        lock (_sync)
        {
            var document = LoadUnsafe();
            change(document);
            Write(document);
        }
    }

    private BackupSettingsDocument LoadUnsafe()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                var document = JsonSerializer.Deserialize<BackupSettingsDocument>(File.ReadAllBytes(FilePath), BackupJson.Options);
                if (document is not null)
                {
                    document.Schedule ??= new BackupSchedule();
                    document.Schedule.Weekdays ??= [];
                    document.State ??= new BackupRunState { ScheduleBaseline = _time.GetUtcNow() };
                    document.State.PendingAudits ??= [];
                    return document;
                }
            }
            catch (JsonException)
            {
                // Keep the unreadable file for inspection and start with defaults.
                File.Copy(FilePath, FilePath + ".invalid", overwrite: true);
            }
        }

        var created = new BackupSettingsDocument { State = new BackupRunState { ScheduleBaseline = _time.GetUtcNow() } };
        Write(created);
        return created;
    }

    private void Write(BackupSettingsDocument document)
    {
        Directory.CreateDirectory(DataDirectory);
        var temp = FilePath + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(document, BackupJson.Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
