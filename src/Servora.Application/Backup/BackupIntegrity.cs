namespace Servora.Application.Backup;

/// <summary>Integrity state of a backup archive as known to the catalog.</summary>
public enum BackupIntegrity
{
    /// <summary>The manifest is readable; the content hashes have not been verified in this session.</summary>
    Unverified = 0,

    /// <summary>All entry hashes (and the signature of encrypted archives) were verified successfully.</summary>
    Valid = 1,

    /// <summary>The archive is unreadable or failed verification.</summary>
    Invalid = 2,
}
