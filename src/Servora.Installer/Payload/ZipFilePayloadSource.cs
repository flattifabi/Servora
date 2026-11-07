using System.IO.Compression;
using Servora.Installer.Abstractions;

namespace Servora.Installer.Payload;

/// <summary>A release zip (<c>Servora-&lt;version&gt;-win-x64.zip</c>) passed with <c>--source</c>.</summary>
/// <param name="zipPath">The full path of the zip file.</param>
internal sealed class ZipFilePayloadSource(string zipPath) : IPayloadSource
{
    /// <inheritdoc />
    public string Description => $"zip file {zipPath}";

    /// <inheritdoc />
    public void ExtractTo(string destinationDirectory) =>
        ZipFile.ExtractToDirectory(zipPath, destinationDirectory, overwriteFiles: false);
}
