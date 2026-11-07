using Microsoft.Extensions.Logging;

namespace Servora.Application.Observability;

/// <summary>A minimum log level for every category starting with <paramref name="Prefix"/>.</summary>
/// <param name="Prefix">The category prefix, for example <c>Servora.Infrastructure</c> or <c>Microsoft.EntityFrameworkCore</c>.</param>
/// <param name="Level">The minimum level.</param>
public sealed record LogCategoryOverride(string Prefix, LogLevel Level);
