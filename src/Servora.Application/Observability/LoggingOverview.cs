using Microsoft.Extensions.Logging;

namespace Servora.Application.Observability;

/// <summary>Everything the log viewer shows besides the entries themselves.</summary>
/// <param name="Levels">The active runtime log levels.</param>
/// <param name="ConfiguredDefault">The default level from configuration.</param>
/// <param name="Statistics">Ring buffer statistics.</param>
/// <param name="Retention">The file retention settings.</param>
/// <param name="LogDirectory">The log directory on disk.</param>
/// <param name="Files">The log files, newest first.</param>
/// <param name="Categories">Categories seen in the buffer.</param>
public sealed record LoggingOverview(
    LogLevelSettings Levels,
    LogLevel ConfiguredDefault,
    LogStoreStatistics Statistics,
    LogRetentionSettings Retention,
    string LogDirectory,
    IReadOnlyList<LogFileInfo> Files,
    IReadOnlyList<string> Categories);
