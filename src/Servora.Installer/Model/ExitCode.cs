namespace Servora.Installer.Model;

/// <summary>Process exit codes of <c>ServoraSetup.exe</c> (documented in docs/operations/installation.md).</summary>
internal enum ExitCode
{
    /// <summary>The command completed successfully.</summary>
    Success = 0,

    /// <summary>The command failed; nothing was rolled back or the rollback was not needed.</summary>
    Failed = 1,

    /// <summary>The command line could not be parsed.</summary>
    InvalidArguments = 2,

    /// <summary>The command needs administrative rights and the process is not elevated.</summary>
    ElevationRequired = 3,

    /// <summary>The command needs an existing installation but the service does not exist.</summary>
    NotInstalled = 4,

    /// <summary>An installation was requested but the service already exists.</summary>
    AlreadyInstalled = 5,

    /// <summary>No application payload (embedded zip, <c>app</c> folder or <c>--source</c>) was found.</summary>
    PayloadMissing = 6,

    /// <summary>The update failed and the previous version was restored.</summary>
    RolledBack = 7,

    /// <summary>The operator cancelled the operation (prompt or UAC dialog).</summary>
    Cancelled = 8,
}
