using Servora.Application.Common;

namespace Servora.Tests.Unit.Configuration;

/// <summary><see cref="IAppPaths"/> rooted in a fresh temporary directory that is deleted on dispose.</summary>
internal sealed class TestAppPaths : IAppPaths, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TestAppPaths"/> class.</summary>
    public TestAppPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "servora-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <inheritdoc />
    public string AttachmentDirectory => Path.Combine(DataDirectory, "attachments");

    /// <inheritdoc />
    public string KeyDirectory => Path.Combine(DataDirectory, "keys");

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of temporary test data.
        }
    }
}
