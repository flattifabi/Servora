namespace Servora.Application.Observability;

/// <summary>Statistics of the in-memory log buffer.</summary>
/// <param name="Capacity">The maximum number of buffered entries.</param>
/// <param name="Count">The number of currently buffered entries.</param>
/// <param name="TotalWritten">The number of entries written since start.</param>
/// <param name="LastSequence">The sequence number of the newest entry (0 when empty).</param>
/// <param name="Errors">Buffered entries with level Error or Critical.</param>
/// <param name="Warnings">Buffered entries with level Warning.</param>
/// <param name="DebugOrTrace">Buffered entries with level Debug or Trace.</param>
public sealed record LogStoreStatistics(int Capacity, int Count, long TotalWritten, long LastSequence, int Errors, int Warnings, int DebugOrTrace);
