using Microsoft.Extensions.Logging;

namespace Servora.Application.Observability;

/// <summary>Requested change of the runtime log levels.</summary>
/// <param name="GlobalLevel">The global minimum level; <see langword="null"/> keeps the configured default.</param>
/// <param name="Overrides">Per category prefix levels.</param>
/// <param name="AutoRevertMinutes">Automatically revert to the previous levels after this many minutes (1..1440); null keeps the change.</param>
public sealed record LogLevelUpdate(LogLevel? GlobalLevel, IReadOnlyList<LogCategoryOverride> Overrides, int? AutoRevertMinutes);
