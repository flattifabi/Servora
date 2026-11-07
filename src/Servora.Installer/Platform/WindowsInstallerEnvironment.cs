using System.Runtime.Versioning;
using System.Security.Principal;
using Servora.Installer.Abstractions;

namespace Servora.Installer.Platform;

/// <summary><see cref="IInstallerEnvironment"/> of the current Windows machine and process.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsInstallerEnvironment : IInstallerEnvironment
{
    /// <inheritdoc />
    public string ProgramFilesDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    /// <inheritdoc />
    public string ProgramDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    /// <inheritdoc />
    public string WindowsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    /// <inheritdoc />
    public string SystemDirectory => Environment.SystemDirectory;

    /// <inheritdoc />
    public string TempDirectory => Path.GetTempPath();

    /// <inheritdoc />
    public string InstallerDirectory => AppContext.BaseDirectory;

    /// <inheritdoc />
    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <inheritdoc />
    public string? GetMachineEnvironmentVariable(string variable) =>
        Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Machine);
}
