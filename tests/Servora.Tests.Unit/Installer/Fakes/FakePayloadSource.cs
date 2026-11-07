using Servora.Installer.Abstractions;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Payload that writes a fixed set of files into the in-memory file system.</summary>
/// <param name="fileSystem">The in-memory file system.</param>
/// <param name="version">The product version of <c>Servora.exe</c>.</param>
/// <param name="files">Relative path → content; <c>Servora.exe</c> is added automatically.</param>
internal sealed class FakePayloadSource(InMemoryFileSystem fileSystem, string version, IReadOnlyDictionary<string, string> files) : IPayloadSource
{
    /// <inheritdoc />
    public string Description => $"test payload {version}";

    /// <summary>Gets or sets a value indicating whether <c>Servora.exe</c> is left out (invalid payload).</summary>
    public bool OmitExecutable { get; set; }

    /// <inheritdoc />
    public void ExtractTo(string destinationDirectory)
    {
        if (!OmitExecutable)
        {
            fileSystem.AddFile(Path.Combine(destinationDirectory, "Servora.exe"), "exe " + version, version);
        }

        foreach (var (relative, content) in files)
        {
            fileSystem.AddFile(Path.Combine(destinationDirectory, relative), content);
        }
    }
}
