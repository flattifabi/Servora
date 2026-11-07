using Microsoft.Extensions.Logging;

namespace Servora.Application.Observability;

/// <summary>Applies runtime log levels to the logging pipeline without restarting the process (singleton).</summary>
public interface ILogLevelController
{
    /// <summary>Gets the active settings.</summary>
    LogLevelSettings Current { get; }

    /// <summary>Gets the default level from the <c>Logging:LogLevel:Default</c> configuration.</summary>
    LogLevel ConfiguredDefaultLevel { get; }

    /// <summary>Applies new settings immediately. Persistence is the caller's responsibility.</summary>
    /// <param name="settings">The settings.</param>
    void Apply(LogLevelSettings settings);
}
