using Servora.Application.Catalog;
using Servora.Application.Common;

namespace Servora.Infrastructure.Catalog;

/// <summary>
/// <see cref="ICatalogImageStore"/> that keeps images as <c>&lt;data dir&gt;/catalog/images/&lt;id&gt;.bin</c>. File names are
/// derived from generated identifiers only, so client input never reaches the file system.
/// </summary>
/// <param name="paths">The application paths.</param>
public sealed class CatalogImageStore(IAppPaths paths) : ICatalogImageStore
{
    private string Directory => Path.Combine(paths.DataDirectory, "catalog", "images");

    /// <inheritdoc />
    public async Task<Guid> SaveAsync(byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        System.IO.Directory.CreateDirectory(Directory);
        var id = Guid.NewGuid();
        var file = FileOf(id);
        var temp = file + ".tmp";
        await File.WriteAllBytesAsync(temp, content, cancellationToken);
        File.Move(temp, file, overwrite: true);
        return id;
    }

    /// <inheritdoc />
    public Stream? Open(Guid imageId)
    {
        var file = FileOf(imageId);
        return File.Exists(file)
            ? new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 16 * 1024, useAsync: true)
            : null;
    }

    /// <inheritdoc />
    public void Delete(Guid imageId)
    {
        var file = FileOf(imageId);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    private string FileOf(Guid imageId) => Path.Combine(Directory, imageId.ToString("N") + ".bin");
}
