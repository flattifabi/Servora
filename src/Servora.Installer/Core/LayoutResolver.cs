using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>Resolves and validates the directories of a Servora installation.</summary>
/// <param name="services">The service manager.</param>
/// <param name="environment">The environment.</param>
internal sealed class LayoutResolver(IServiceManager services, IInstallerEnvironment environment)
{
    /// <summary>Gets the default install directory (<c>%ProgramFiles%\Servora</c>).</summary>
    public string DefaultInstallDirectory => Path.Combine(environment.ProgramFilesDirectory, ProductInfo.FolderName);

    /// <summary>Gets the default data directory (<c>%ProgramData%\Servora</c>).</summary>
    public string DefaultDataDirectory => Path.Combine(environment.ProgramDataDirectory, ProductInfo.FolderName);

    /// <summary>Gets the backup root (<c>%ProgramData%\Servora\backups</c>).</summary>
    public string BackupDirectory => Path.Combine(DefaultDataDirectory, ProductInfo.BackupsFolderName);

    /// <summary>
    /// Resolves the data directory the service uses, with the same precedence as the host (<c>AppPaths</c>):
    /// service environment <c>SERVORA_DATA_DIR</c>, machine environment <c>SERVORA_DATA_DIR</c>, <c>%ProgramData%\Servora</c>.
    /// </summary>
    /// <returns>The full data directory path.</returns>
    public string ResolveDataDirectory()
    {
        var configured = services.GetEnvironmentVariable(ProductInfo.ServiceName, ProductInfo.DataDirectoryVariable)
            ?? environment.GetMachineEnvironmentVariable(ProductInfo.DataDirectoryVariable);
        return string.IsNullOrWhiteSpace(configured) ? DefaultDataDirectory : Normalize(configured);
    }

    /// <summary>Resolves the layout of an installed service from its registered image path.</summary>
    /// <param name="service">The installed service.</param>
    /// <returns>The layout.</returns>
    public InstallerLayout ForInstalledService(ServiceInfo service)
    {
        var executable = ExecutablePathFromImagePath(service.BinaryPath);
        var installDirectory = Path.GetDirectoryName(executable) ?? DefaultInstallDirectory;
        return new InstallerLayout(installDirectory, ResolveDataDirectory(), BackupDirectory);
    }

    /// <summary>Extracts the executable path from a service image path (quoted or unquoted, optional arguments).</summary>
    /// <param name="imagePath">The registered image path.</param>
    /// <returns>The executable path.</returns>
    public static string ExecutablePathFromImagePath(string imagePath)
    {
        var text = Environment.ExpandEnvironmentVariables(imagePath.Trim());
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 0 ? text[1..end] : text.Trim('"');
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe >= 0 ? text[..(exe + 4)] : text;
    }

    /// <summary>Validates an install directory chosen by the operator.</summary>
    /// <param name="installDirectory">The candidate install directory.</param>
    /// <param name="dataDirectory">The data directory.</param>
    /// <returns>An error message, or <see langword="null"/> when valid.</returns>
    public string? ValidateInstallDirectory(string installDirectory, string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || !Path.IsPathFullyQualified(installDirectory))
        {
            return "The install directory must be an absolute path such as C:\\Program Files\\Servora.";
        }

        var full = Normalize(installDirectory);
        if (string.Equals(Path.GetPathRoot(full), full, StringComparison.OrdinalIgnoreCase))
        {
            return "The install directory must not be the root of a drive.";
        }

        if (IsSameOrInside(full, environment.WindowsDirectory))
        {
            return "The install directory must not be inside the Windows directory.";
        }

        if (IsSameOrInside(full, Normalize(dataDirectory)) || IsSameOrInside(Normalize(dataDirectory), full))
        {
            return "The install directory and the data directory must be separate folders (the install directory is replaced on update).";
        }

        if (string.Equals(full, Normalize(environment.ProgramFilesDirectory), StringComparison.OrdinalIgnoreCase)
            || string.Equals(full, Normalize(environment.ProgramDataDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return "Choose a dedicated sub-folder, for example C:\\Program Files\\Servora.";
        }

        return null;
    }

    /// <summary>
    /// Gets the top-level entries of the install directory that are data and must survive an update:
    /// <c>App_Data</c> and the data directory itself when an operator placed it inside the install directory.
    /// </summary>
    /// <param name="installDirectory">The install directory.</param>
    /// <param name="dataDirectory">The data directory.</param>
    /// <returns>The preserved top-level names (case-insensitive).</returns>
    public static IReadOnlySet<string> PreservedEntries(string installDirectory, string dataDirectory)
    {
        var preserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ProductInfo.LegacyDataFolderName };
        var install = Normalize(installDirectory);
        var data = Normalize(dataDirectory);
        if (IsSameOrInside(data, install) && !string.Equals(data, install, StringComparison.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(install, data);
            preserved.Add(relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0]);
        }

        return preserved;
    }

    /// <summary>Normalizes a path (full path, no trailing separator).</summary>
    /// <param name="path">The path.</param>
    /// <returns>The normalized path.</returns>
    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path.Trim().Trim('"'));
        var root = Path.GetPathRoot(full);
        return full.Length > (root?.Length ?? 0) ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
    }

    /// <summary>Determines whether <paramref name="path"/> equals or is inside <paramref name="parent"/>.</summary>
    /// <param name="path">The candidate path (normalized).</param>
    /// <param name="parent">The parent path.</param>
    /// <returns><see langword="true"/> when inside or equal.</returns>
    public static bool IsSameOrInside(string path, string parent)
    {
        var p = Normalize(parent);
        return string.Equals(path, p, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(p.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
