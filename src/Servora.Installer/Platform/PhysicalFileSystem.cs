using System.Diagnostics;
using System.Text;
using Servora.Installer.Abstractions;

namespace Servora.Installer.Platform;

/// <summary><see cref="IFileSystem"/> on the real disk.</summary>
internal sealed class PhysicalFileSystem : IFileSystem
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc />
    public void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
            // Read-only files block a recursive delete; clear the attribute and try once more.
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
    }

    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    /// <inheritdoc />
    public void CopyFile(string sourcePath, string destinationPath) => File.Copy(sourcePath, destinationPath, overwrite: true);

    /// <inheritdoc />
    public IReadOnlyList<string> GetFiles(string path) => Directory.GetFiles(path);

    /// <inheritdoc />
    public IReadOnlyList<string> GetDirectories(string path) =>
        new DirectoryInfo(path).GetDirectories()
            .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0)
            .Select(d => d.FullName)
            .ToList();

    /// <inheritdoc />
    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

    /// <inheritdoc />
    public void WriteAllText(string path, string content) => File.WriteAllText(path, content, Utf8NoBom);

    /// <inheritdoc />
    public string? GetProductVersion(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = FileVersionInfo.GetVersionInfo(path);
        return string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
    }
}
