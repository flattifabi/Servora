using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>Overlays the runtime debug switch on the logger filter options built from configuration.</summary>
/// <param name="levelSwitch">The level switch.</param>
internal sealed class LogLevelFilterPostConfigure(LogLevelSwitch levelSwitch) : IPostConfigureOptions<LoggerFilterOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, LoggerFilterOptions options) => LogLevelSwitch.ApplyTo(options, levelSwitch.Current);
}
