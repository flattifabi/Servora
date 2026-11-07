namespace Servora.Domain.Backup;

/// <summary>Why a backup archive was created. Drives retention and how the backup is presented.</summary>
public enum BackupKind
{
    /// <summary>Created by the backup schedule.</summary>
    Scheduled = 0,

    /// <summary>Created on demand ("Back up now" or the command line).</summary>
    Manual = 1,

    /// <summary>Safety backup created automatically before database migrations are applied.</summary>
    PreMigration = 2,

    /// <summary>Safety backup created automatically before a restore replaces the database.</summary>
    PreRestore = 3,

    /// <summary>An archive uploaded by an administrator (for example from another installation).</summary>
    Uploaded = 4,
}
