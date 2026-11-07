using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>
/// Servora's own logger provider (alias <c>Servora</c>): every log event that passes the logging filters (including the
/// runtime debug switch) is redacted once and then written to the in-memory ring buffer (log viewer / live tail) and the
/// rolling JSON-lines files in <c>&lt;data dir&gt;/logs</c>. Scopes (for example the request correlation id) are captured.
/// </summary>
/// <param name="memorySink">The ring buffer.</param>
/// <param name="fileSink">The file writer.</param>
[ProviderAlias("Servora")]
public sealed class ServoraLoggerProvider(InMemoryLogSink memorySink, JsonFileLogSink fileSink) : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, ServoraLogger> _loggers = new(StringComparer.Ordinal);
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    /// <summary>Gets the ring buffer.</summary>
    internal InMemoryLogSink MemorySink => memorySink;

    /// <summary>Gets the file writer.</summary>
    internal JsonFileLogSink FileSink => fileSink;

    /// <summary>Gets the scope provider shared with the logger factory.</summary>
    internal IExternalScopeProvider ScopeProvider => _scopeProvider;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new ServoraLogger(name, this));

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    /// <inheritdoc />
    public void Dispose() => _loggers.Clear();
}
