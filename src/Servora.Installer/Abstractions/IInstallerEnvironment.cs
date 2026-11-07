namespace Servora.Installer.Abstractions;

/// <summary>Machine facts the installer depends on (special folders, elevation, environment). Faked in unit tests.</summary>
internal interface IInstallerEnvironment
{
    /// <summary>Gets the 64-bit program files directory (<c>%ProgramFiles%</c>).</summary>
    string ProgramFilesDirectory { get; }

    /// <summary>Gets the machine-wide application data directory (<c>%ProgramData%</c>).</summary>
    string ProgramDataDirectory { get; }

    /// <summary>Gets the Windows directory (<c>%SystemRoot%</c>).</summary>
    string WindowsDirectory { get; }

    /// <summary>Gets the system directory with <c>sc.exe</c>, <c>netsh.exe</c> and <c>icacls.exe</c>.</summary>
    string SystemDirectory { get; }

    /// <summary>Gets the temporary directory used for staging the payload.</summary>
    string TempDirectory { get; }

    /// <summary>Gets the directory that contains <c>ServoraSetup.exe</c>.</summary>
    string InstallerDirectory { get; }

    /// <summary>Gets a value indicating whether the process runs with administrative rights.</summary>
    bool IsElevated { get; }

    /// <summary>Reads a machine-level environment variable.</summary>
    /// <param name="variable">The variable name.</param>
    /// <returns>The value, or <see langword="null"/> when not set.</returns>
    string? GetMachineEnvironmentVariable(string variable);
}
