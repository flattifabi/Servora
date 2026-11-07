using Servora.Installer.Model;

namespace Servora.Installer.Cli;

/// <summary>Parsed command line of <c>ServoraSetup.exe</c>.</summary>
internal sealed record InstallerOptions
{
    /// <summary>Gets the command.</summary>
    public InstallerCommand Command { get; init; } = InstallerCommand.Auto;

    /// <summary>Gets a value indicating whether all prompts are answered with defaults (unattended mode).</summary>
    public bool Silent { get; init; }

    /// <summary>Gets the HTTP port for a new installation (<see langword="null"/> = keep existing or default 5080).</summary>
    public int? Port { get; init; }

    /// <summary>Gets the install directory for a new installation (<see langword="null"/> = <c>%ProgramFiles%\Servora</c>).</summary>
    public string? InstallDirectory { get; init; }

    /// <summary>Gets a custom data directory for a new installation (sets <c>SERVORA_DATA_DIR</c> for the service).</summary>
    public string? DataDirectory { get; init; }

    /// <summary>Gets a value indicating whether <c>uninstall</c> keeps the data directory.</summary>
    public bool KeepData { get; init; }

    /// <summary>Gets the firewall choice (<see langword="null"/> = ask interactively, no rule in silent mode).</summary>
    public bool? Firewall { get; init; }

    /// <summary>Gets a value indicating whether the browser is not opened after installation.</summary>
    public bool NoBrowser { get; init; }

    /// <summary>Gets a value indicating whether safety checks (downgrade, port in use) are overridden.</summary>
    public bool Force { get; init; }

    /// <summary>Gets the number of binary backups kept after a successful update.</summary>
    public int KeepBackups { get; init; } = ProductInfo.DefaultBackupsToKeep;

    /// <summary>Gets the health check timeout override.</summary>
    public TimeSpan? HealthTimeout { get; init; }

    /// <summary>Gets an explicit health check URL (for HTTPS-only or reverse-proxy setups).</summary>
    public Uri? HealthUrl { get; init; }

    /// <summary>Gets an explicit payload (published folder or zip file).</summary>
    public string? Source { get; init; }

    /// <summary>Gets the service account for a new installation.</summary>
    public ServiceAccountKind ServiceAccount { get; init; } = ServiceAccountKind.Virtual;

    /// <summary>Gets a value indicating whether this process was relaunched through UAC (pause before exit).</summary>
    public bool Elevated { get; init; }
}
