using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>
/// Holds the runtime log level settings (debug switch) and signals changes to the logging infrastructure through a
/// change token, so <see cref="LoggerFilterOptions"/> are recomputed and every logger picks up the new levels
/// immediately — without restarting the process.
/// </summary>
public sealed class LogLevelSwitch : ILogLevelController
{
    private readonly object _sync = new();
    private LogLevelSettings _current = LogLevelSettings.Defaults;
    private CancellationTokenSource _changeSource = new();

    /// <summary>Initializes a new instance of the <see cref="LogLevelSwitch"/> class.</summary>
    /// <param name="configuration">The application configuration (reads <c>Logging:LogLevel:Default</c>).</param>
    public LogLevelSwitch(IConfiguration? configuration = null)
    {
        ConfiguredDefaultLevel = Enum.TryParse<LogLevel>(configuration?["Logging:LogLevel:Default"], ignoreCase: true, out var level)
            ? level
            : LogLevel.Information;
    }

    /// <inheritdoc />
    public LogLevelSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public LogLevel ConfiguredDefaultLevel { get; }

    /// <inheritdoc />
    public void Apply(LogLevelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        CancellationTokenSource previous;
        lock (_sync)
        {
            _current = Normalize(settings);
            previous = _changeSource;
            _changeSource = new CancellationTokenSource();
        }

        // Signal outside the lock: listeners recompute the filter options and call back into Current.
        previous.Cancel();
        previous.Dispose();
    }

    /// <summary>Gets a change token that fires on the next <see cref="Apply"/>.</summary>
    /// <returns>The change token.</returns>
    public IChangeToken GetChangeToken()
    {
        lock (_sync)
        {
            return new CancellationChangeToken(_changeSource.Token);
        }
    }

    /// <summary>Applies the settings to logger filter options (called by the post-configure step).</summary>
    /// <param name="options">The options produced from configuration.</param>
    /// <param name="settings">The runtime settings.</param>
    public static void ApplyTo(LoggerFilterOptions options, LogLevelSettings settings)
    {
        if (settings.GlobalLevel is { } global)
        {
            options.MinLevel = global;
            var replaced = false;
            for (var i = 0; i < options.Rules.Count; i++)
            {
                var rule = options.Rules[i];
                if (rule.ProviderName is null && rule.CategoryName is null)
                {
                    options.Rules[i] = new LoggerFilterRule(null, null, global, rule.Filter);
                    replaced = true;
                }
            }

            if (!replaced)
            {
                options.Rules.Add(new LoggerFilterRule(null, null, global, null));
            }
        }

        foreach (var categoryOverride in settings.Overrides)
        {
            // An override wins over configured rules for the same prefix or any more specific category below it.
            for (var i = options.Rules.Count - 1; i >= 0; i--)
            {
                var rule = options.Rules[i];
                if (rule.ProviderName is null && rule.CategoryName is not null
                    && rule.CategoryName.StartsWith(categoryOverride.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    options.Rules.RemoveAt(i);
                }
            }

            options.Rules.Add(new LoggerFilterRule(null, categoryOverride.Prefix, categoryOverride.Level, null));
        }
    }

    private static LogLevelSettings Normalize(LogLevelSettings settings) => settings with
    {
        Overrides = settings.Overrides
            .Where(o => !string.IsNullOrWhiteSpace(o.Prefix))
            .Select(o => o with { Prefix = o.Prefix.Trim() })
            .GroupBy(o => o.Prefix, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .OrderBy(o => o.Prefix.Length)
            .Take(50)
            .ToList(),
    };
}
