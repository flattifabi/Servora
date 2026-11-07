using Servora.Installer.Abstractions;

namespace Servora.Installer.Payload;

/// <summary>A folder with the published application (side-by-side <c>app</c> folder or <c>--source &lt;folder&gt;</c>).</summary>
/// <param name="directory">The full path of the folder.</param>
internal sealed class DirectoryPayloadSource(string directory) : IPayloadSource
{
    /// <inheritdoc />
    public string Description => $"folder {directory}";

    /// <inheritdoc />
    public void ExtractTo(string destinationDirectory) => CopyTree(new DirectoryInfo(directory), destinationDirectory);

    private static void CopyTree(DirectoryInfo source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in source.EnumerateFiles())
        {
            file.CopyTo(Path.Combine(destination, file.Name), overwrite: false);
        }

        foreach (var sub in source.EnumerateDirectories())
        {
            // Do not follow junctions or symbolic links out of the payload folder.
            if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                CopyTree(sub, Path.Combine(destination, sub.Name));
            }
        }
    }
}
