using Servora.Application.Common;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary><see cref="IAppPaths"/> rooted in a temporary directory that is deleted on dispose.</summary>
internal sealed class TestAppPaths : IAppPaths, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TestAppPaths"/> class.</summary>
    public TestAppPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "servora-backup-tests", Guid.NewGuid().ToString("N"));
        LogDirectory = Path.Combine(DataDirectory, "logs");
        AttachmentDirectory = Path.Combine(DataDirectory, "attachments");
        KeyDirectory = Path.Combine(DataDirectory, "keys");
        foreach (var directory in new[] { DataDirectory, LogDirectory, AttachmentDirectory, KeyDirectory })
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string LogDirectory { get; }

    /// <inheritdoc />
    public string AttachmentDirectory { get; }

    /// <inheritdoc />
    public string KeyDirectory { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temporary directory.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup of the temporary directory.
        }
    }
}
