using Microsoft.Extensions.Logging;

namespace Servora.Application.Observability;

/// <summary>
/// Runtime log level configuration (the "debug switch"). Applied on top of the <c>Logging</c> configuration without
/// restart and persisted as a system setting.
/// </summary>
public sealed record LogLevelSettings
{
    /// <summary>Gets the configuration defaults (no runtime overrides).</summary>
    public static LogLevelSettings Defaults { get; } = new();

    /// <summary>Gets the global minimum level; <see langword="null"/> keeps the configured default.</summary>
    public LogLevel? GlobalLevel { get; init; }

    /// <summary>Gets the per category prefix overrides.</summary>
    public IReadOnlyList<LogCategoryOverride> Overrides { get; init; } = [];

    /// <summary>Gets the UTC time at which the settings automatically revert to <see cref="RevertTo"/>.</summary>
    public DateTimeOffset? RevertAt { get; init; }

    /// <summary>Gets the settings restored when <see cref="RevertAt"/> is reached (defaults when null).</summary>
    public LogLevelSettings? RevertTo { get; init; }

    /// <summary>Gets a value indicating whether any runtime override is active.</summary>
    public bool HasOverrides => GlobalLevel is not null || Overrides.Count > 0;

    /// <summary>Gets a value indicating whether verbose (Debug or Trace) logging is enabled globally or for any category.</summary>
    public bool IsVerbose => GlobalLevel is LogLevel.Debug or LogLevel.Trace || Overrides.Any(o => o.Level is LogLevel.Debug or LogLevel.Trace);
}
