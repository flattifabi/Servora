namespace Servora.Application.Observability;

/// <summary>In-memory ring buffer of the most recent log entries (implemented by the logger provider).</summary>
public interface ILogStore
{
    /// <summary>Queries buffered entries, newest first.</summary>
    /// <param name="query">The filter.</param>
    /// <returns>The matching entries (at most <see cref="LogQuery.Take"/>).</returns>
    IReadOnlyList<LogEntry> Query(LogQuery query);

    /// <summary>Gets a buffered entry by sequence number.</summary>
    /// <param name="sequence">The sequence number.</param>
    /// <returns>The entry, or <see langword="null"/> when it was evicted.</returns>
    LogEntry? Get(long sequence);

    /// <summary>Gets buffer statistics.</summary>
    /// <returns>The statistics.</returns>
    LogStoreStatistics GetStatistics();

    /// <summary>Gets the distinct categories of the buffered entries, ordered by name.</summary>
    /// <param name="max">The maximum number of categories.</param>
    /// <returns>The categories.</returns>
    IReadOnlyList<string> GetCategories(int max = 200);
}
