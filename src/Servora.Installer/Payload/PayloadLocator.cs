using System.Reflection;
using Servora.Installer.Abstractions;
using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Installer.Payload;

/// <summary>
/// Chooses the payload: <c>--source</c> (folder or zip) first, then the zip embedded into the installer, then an
/// <c>app</c> folder next to <c>ServoraSetup.exe</c>.
/// </summary>
/// <param name="environment">The environment (installer directory).</param>
/// <param name="assembly">The assembly that may carry the embedded payload.</param>
internal sealed class PayloadLocator(IInstallerEnvironment environment, Assembly assembly)
{
    /// <summary>Locates the payload.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The payload source, or <see langword="null"/> when none exists.</returns>
    /// <exception cref="InstallerException">The <c>--source</c> path does not exist.</exception>
    public IPayloadSource? Locate(InstallerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Source))
        {
            var source = Path.GetFullPath(options.Source.Trim('"'));
            if (Directory.Exists(source))
            {
                return new DirectoryPayloadSource(source);
            }

            if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return new ZipFilePayloadSource(source);
            }

            throw new InstallerException($"--source '{source}' is neither a folder nor a .zip file.", ExitCode.PayloadMissing);
        }

        if (EmbeddedPayloadSource.IsPresent(assembly))
        {
            return new EmbeddedPayloadSource(assembly);
        }

        var sideBySide = Path.Combine(environment.InstallerDirectory, ProductInfo.SideBySidePayloadFolder);
        return File.Exists(Path.Combine(sideBySide, ProductInfo.ExecutableName)) ? new DirectoryPayloadSource(sideBySide) : null;
    }
}
