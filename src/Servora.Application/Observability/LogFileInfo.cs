namespace Servora.Application.Observability;

/// <summary>A log file in the data directory.</summary>
/// <param name="Name">The file name (no directory).</param>
/// <param name="SizeBytes">The size in bytes.</param>
/// <param name="LastWriteUtc">The last write time (UTC).</param>
public sealed record LogFileInfo(string Name, long SizeBytes, DateTimeOffset LastWriteUtc);
