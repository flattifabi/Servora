using Servora.Application.Common;

namespace Servora.Infrastructure.Hosting;

/// <summary>
/// Resolves and creates the data directories. Order of precedence: <c>SERVORA_DATA_DIR</c> environment variable,
/// <c>%ProgramData%\Servora</c> when running as Windows service, otherwise <c>&lt;content root&gt;/App_Data</c>.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    /// <summary>Name of the environment variable that overrides the data directory.</summary>
    public const string DataDirectoryVariable = "SERVORA_DATA_DIR";

    /// <summary>Initializes a new instance of the <see cref="AppPaths"/> class and creates missing directories.</summary>
    /// <param name="contentRoot">The application content root.</param>
    /// <param name="isWindowsService">Whether the process runs as a Windows service.</param>
    public AppPaths(string contentRoot, bool isWindowsService)
    {
        var configured = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        DataDirectory = !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured)
            : isWindowsService
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Servora")
                : Path.Combine(contentRoot, "App_Data");

        LogDirectory = Path.Combine(DataDirectory, "logs");
        AttachmentDirectory = Path.Combine(DataDirectory, "attachments");
        KeyDirectory = Path.Combine(DataDirectory, "keys");

        foreach (var directory in new[] { DataDirectory, LogDirectory, AttachmentDirectory, KeyDirectory })
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string LogDirectory { get; }

    /// <inheritdoc />
    public string AttachmentDirectory { get; }

    /// <inheritdoc />
    public string KeyDirectory { get; }
}
