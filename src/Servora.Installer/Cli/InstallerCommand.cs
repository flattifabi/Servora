namespace Servora.Installer.Cli;

/// <summary>Top-level command of <c>ServoraSetup.exe</c>.</summary>
internal enum InstallerCommand
{
    /// <summary>No command given: interactive menu, or automatic install/update in silent mode.</summary>
    Auto,

    /// <summary>Fresh installation.</summary>
    Install,

    /// <summary>In-place update of an existing installation with backup and rollback.</summary>
    Update,

    /// <summary>Removal of the service and program files (and the data unless <c>--keep-data</c>).</summary>
    Uninstall,

    /// <summary>Read-only status report (works without elevation).</summary>
    Status,

    /// <summary>Usage information.</summary>
    Help,

    /// <summary>Installer and payload version.</summary>
    Version,
}
