using Servora.Application.Common;

namespace Servora.Tests.Unit.Portal;

/// <summary>Application paths below a temporary folder that is deleted on dispose.</summary>
public sealed class PortalTestPaths : IAppPaths, IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="PortalTestPaths"/> class.</summary>
    public PortalTestPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "servora-portal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(AttachmentDirectory);
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
            // Best effort clean-up of the temporary folder.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort clean-up of the temporary folder.
        }
    }
}
