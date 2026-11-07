using Servora.Installer.Abstractions;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Fixed machine layout for workflow tests.</summary>
internal sealed class FakeInstallerEnvironment : IInstallerEnvironment
{
    /// <inheritdoc />
    public string ProgramFilesDirectory => @"C:\Program Files";

    /// <inheritdoc />
    public string ProgramDataDirectory => @"C:\ProgramData";

    /// <inheritdoc />
    public string WindowsDirectory => @"C:\Windows";

    /// <inheritdoc />
    public string SystemDirectory => @"C:\Windows\System32";

    /// <inheritdoc />
    public string TempDirectory => @"C:\Temp";

    /// <inheritdoc />
    public string InstallerDirectory { get; set; } = @"C:\Downloads";

    /// <inheritdoc />
    public bool IsElevated { get; set; } = true;

    /// <summary>Gets the machine environment variables.</summary>
    public Dictionary<string, string> MachineVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string? GetMachineEnvironmentVariable(string variable) =>
        MachineVariables.TryGetValue(variable, out var value) ? value : null;
}
