using Servora.Installer.Abstractions;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Environment whose temporary directory is a test-owned folder (for real file system tests).</summary>
/// <param name="tempDirectory">The temporary directory.</param>
internal sealed class TempEnvironment(string tempDirectory) : IInstallerEnvironment
{
    /// <inheritdoc />
    public string ProgramFilesDirectory => Path.Combine(tempDirectory, "pf");

    /// <inheritdoc />
    public string ProgramDataDirectory => Path.Combine(tempDirectory, "pd");

    /// <inheritdoc />
    public string WindowsDirectory => Path.Combine(tempDirectory, "win");

    /// <inheritdoc />
    public string SystemDirectory => Path.Combine(tempDirectory, "win", "System32");

    /// <inheritdoc />
    public string TempDirectory => tempDirectory;

    /// <inheritdoc />
    public string InstallerDirectory => tempDirectory;

    /// <inheritdoc />
    public bool IsElevated => false;

    /// <inheritdoc />
    public string? GetMachineEnvironmentVariable(string variable) => null;
}
