using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>
/// Notifies the options monitor of <see cref="LoggerFilterOptions"/> when the debug switch changes; the logger factory
/// then refreshes the filters of every existing logger.
/// </summary>
/// <param name="levelSwitch">The level switch.</param>
internal sealed class LogLevelChangeTokenSource(LogLevelSwitch levelSwitch) : IOptionsChangeTokenSource<LoggerFilterOptions>
{
    /// <inheritdoc />
    public string? Name => Options.DefaultName;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => levelSwitch.GetChangeToken();
}
