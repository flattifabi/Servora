namespace Servora.Application.Integration.Imports;

/// <summary>A parsed CSV file.</summary>
/// <param name="Headers">The column names (unique, trimmed).</param>
/// <param name="Rows">The data rows (each with exactly <paramref name="Headers"/>.Count values).</param>
/// <param name="Delimiter">The detected delimiter.</param>
/// <param name="Truncated">Whether rows beyond the limit were ignored.</param>
public sealed record CsvDocument(IReadOnlyList<string> Headers, IReadOnlyList<string[]> Rows, char Delimiter, bool Truncated);
