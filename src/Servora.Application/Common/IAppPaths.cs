namespace Servora.Application.Common;

/// <summary>
/// Well-known file system locations of the installation. The data directory defaults to
/// <c>%ProgramData%\Servora</c> when running as a Windows service and to <c>./App_Data</c> during development; it
/// can be overridden with the <c>SERVORA_DATA_DIR</c> environment variable.
/// </summary>
public interface IAppPaths
{
    /// <summary>Gets the root data directory (configuration, SQLite database, keys, logs, attachments).</summary>
    string DataDirectory { get; }

    /// <summary>Gets the directory for log files.</summary>
    string LogDirectory { get; }

    /// <summary>Gets the directory for attachment binaries.</summary>
    string AttachmentDirectory { get; }

    /// <summary>Gets the directory holding Data Protection key material.</summary>
    string KeyDirectory { get; }
}
