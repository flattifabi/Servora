using System.Globalization;
using Microsoft.Extensions.Logging;
using Servora.Application.Observability;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>
/// Logger created by <see cref="ServoraLoggerProvider"/>. Converts a log call into a redacted, size-bounded
/// <see cref="LogEntry"/> and hands it to the ring buffer and the file writer.
/// </summary>
/// <param name="category">The category name.</param>
/// <param name="provider">The owning provider.</param>
internal sealed class ServoraLogger(string category, ServoraLoggerProvider provider) : ILogger
{
    /// <summary>Maximum length of a rendered message.</summary>
    internal const int MaxMessageLength = 16_000;

    /// <summary>Maximum length of exception text.</summary>
    internal const int MaxExceptionLength = 32_000;

    /// <summary>Maximum number of captured structured properties.</summary>
    internal const int MaxProperties = 32;

    /// <summary>Maximum length of one property value.</summary>
    internal const int MaxPropertyLength = 2_000;

    /// <summary>Property / scope key carrying the correlation id.</summary>
    internal const string CorrelationIdKey = "CorrelationId";

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => provider.ScopeProvider.Push(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message;
        try
        {
            message = formatter(state, exception);
        }
        catch (FormatException)
        {
            message = state?.ToString() ?? string.Empty;
        }

        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            Collect(values, properties);
        }

        provider.ScopeProvider.ForEachScope(
            static (scope, target) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                {
                    Collect(scopeValues, target);
                }
            },
            properties);

        properties.Remove(CorrelationIdKey, out var correlationId);

        var entry = new LogEntry(
            provider.MemorySink.NextSequence(),
            DateTimeOffset.UtcNow,
            logLevel,
            category,
            eventId.Id,
            eventId.Name,
            Truncate(SecretRedactor.Redact(message), MaxMessageLength) ?? string.Empty,
            exception is null ? null : Truncate(SecretRedactor.Redact(exception.ToString()), MaxExceptionLength),
            correlationId,
            properties);

        provider.MemorySink.Append(entry);
        provider.FileSink.Enqueue(entry);
    }

    private static void Collect(IEnumerable<KeyValuePair<string, object?>> values, Dictionary<string, string?> properties)
    {
        foreach (var (key, value) in values)
        {
            if (key == "{OriginalFormat}")
            {
                continue;
            }

            if (string.Equals(key, CorrelationIdKey, StringComparison.OrdinalIgnoreCase))
            {
                // Stored under a fixed key and moved to LogEntry.CorrelationId by the caller (innermost value wins).
                if (value is not null && !properties.ContainsKey(CorrelationIdKey))
                {
                    properties[CorrelationIdKey] = Truncate(value.ToString(), 64);
                }

                continue;
            }

            if (properties.Count >= MaxProperties || properties.ContainsKey(key))
            {
                continue;
            }

            properties[key] = SecretRedactor.IsSensitiveKey(key)
                ? SecretRedactor.Mask
                : Truncate(SecretRedactor.Redact(Convert.ToString(value, CultureInfo.InvariantCulture)), MaxPropertyLength);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : string.Concat(value.AsSpan(0, max), "… [truncated]");
}
