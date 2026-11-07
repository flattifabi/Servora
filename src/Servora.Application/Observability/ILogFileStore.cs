namespace Servora.Application.Observability;

/// <summary>Rolling JSON-lines log files in <c>&lt;data dir&gt;/logs</c> (implemented by the logger provider).</summary>
public interface ILogFileStore
{
    /// <summary>Gets the log directory.</summary>
    string Directory { get; }

    /// <summary>Gets the active retention settings.</summary>
    LogRetentionSettings Retention { get; }

    /// <summary>Lists the log files, newest first.</summary>
    /// <returns>The files.</returns>
    IReadOnlyList<LogFileInfo> ListFiles();

    /// <summary>Opens a log file for reading. Only plain file names of Servora log files are accepted.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The stream, or <see langword="null"/> when the name is invalid or the file does not exist.</returns>
    Stream? OpenRead(string name);

    /// <summary>Applies new retention / rolling settings.</summary>
    /// <param name="settings">The settings.</param>
    void Configure(LogRetentionSettings settings);

    /// <summary>Deletes log files older than the retention period.</summary>
    /// <returns>The number of deleted files.</returns>
    int ApplyRetention();
}
