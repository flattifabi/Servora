using System.IO.Compression;
using System.Reflection;
using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Payload;

/// <summary>
/// The payload zip embedded into <c>ServoraSetup.exe</c> at release build time (manifest resource
/// <c>Servora.Payload.zip</c>, see the installer project file).
/// </summary>
/// <param name="assembly">The assembly that carries the resource.</param>
internal sealed class EmbeddedPayloadSource(Assembly assembly) : IPayloadSource
{
    /// <inheritdoc />
    public string Description => "embedded payload";

    /// <summary>Determines whether an assembly carries the payload resource.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns><see langword="true"/> when the resource exists.</returns>
    public static bool IsPresent(Assembly assembly) => assembly.GetManifestResourceInfo(ProductInfo.PayloadResourceName) is not null;

    /// <inheritdoc />
    public void ExtractTo(string destinationDirectory)
    {
        using var stream = assembly.GetManifestResourceStream(ProductInfo.PayloadResourceName)
            ?? throw new InstallerException("The embedded payload is missing.", ExitCode.PayloadMissing);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        // ExtractToDirectory rejects entries that would escape the destination ("zip slip").
        archive.ExtractToDirectory(destinationDirectory, overwriteFiles: false);
    }
}
