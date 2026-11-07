using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Extracts the payload into a temporary staging directory and validates it before anything on the machine is
/// changed. A payload whose files sit in a single top-level folder (a zipped folder) is accepted as well.
/// </summary>
/// <param name="fileSystem">The file system.</param>
/// <param name="environment">The environment (temporary directory).</param>
internal sealed class PayloadStager(IFileSystem fileSystem, IInstallerEnvironment environment)
{
    /// <summary>Extracts and validates the payload.</summary>
    /// <param name="payload">The payload source.</param>
    /// <returns>The staged payload.</returns>
    /// <exception cref="InstallerException">The payload is invalid.</exception>
    public StagedPayload Stage(IPayloadSource payload)
    {
        var root = Path.Combine(environment.TempDirectory, "ServoraSetup-" + Guid.NewGuid().ToString("N"));
        fileSystem.CreateDirectory(root);
        try
        {
            payload.ExtractTo(root);
            var applicationDirectory = FindApplicationDirectory(root)
                ?? throw new InstallerException(
                    $"The payload ({payload.Description}) does not contain {ProductInfo.ExecutableName}. Use a Servora-<version>-win-x64.zip from GitHub Releases.",
                    ExitCode.PayloadMissing);
            var version = fileSystem.GetProductVersion(Path.Combine(applicationDirectory, ProductInfo.ExecutableName));
            return new StagedPayload(root, applicationDirectory, version);
        }
        catch
        {
            Cleanup(root);
            throw;
        }
    }

    /// <summary>Deletes a staging directory (best effort).</summary>
    /// <param name="stagingRoot">The staging root.</param>
    public void Cleanup(string stagingRoot)
    {
        try
        {
            fileSystem.DeleteDirectory(stagingRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Temporary files are removed by Windows eventually; never fail the installation because of them.
        }
    }

    private string? FindApplicationDirectory(string root)
    {
        if (fileSystem.FileExists(Path.Combine(root, ProductInfo.ExecutableName)))
        {
            return root;
        }

        var directories = fileSystem.GetDirectories(root);
        return directories.Count == 1 && fileSystem.FileExists(Path.Combine(directories[0], ProductInfo.ExecutableName))
            ? directories[0]
            : null;
    }
}
