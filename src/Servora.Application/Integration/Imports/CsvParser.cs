using System.Text;
using Servora.Application.Common;

namespace Servora.Application.Integration.Imports;

/// <summary>
/// RFC 4180 CSV parser: header row, quoted fields with escaped quotes and line breaks, CRLF / LF line endings, UTF-8
/// BOM, and delimiter detection (comma, semicolon, tab, pipe). Bounded by row, column and field size limits.
/// </summary>
public static class CsvParser
{
    /// <summary>The maximum number of columns.</summary>
    public const int MaxColumns = 200;

    /// <summary>The maximum field length.</summary>
    public const int MaxFieldLength = 32_000;

    private static readonly char[] s_delimiters = [',', ';', '\t', '|'];

    /// <summary>Parses CSV text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxRows">The maximum number of data rows.</param>
    /// <param name="delimiter">The delimiter, or <see langword="null"/> to detect it from the header line.</param>
    /// <returns>The document or a validation failure.</returns>
    public static Result<CsvDocument> Parse(string text, int maxRows, char? delimiter = null)
    {
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<CsvDocument>.Invalid("The file is empty.");
        }

        var separator = delimiter ?? DetectDelimiter(text);
        var records = new List<List<string>>();
        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var truncated = false;
        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(ch);
                i++;
            }
            else if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
                i++;
            }
            else if (ch == separator)
            {
                current.Add(field.ToString());
                field.Clear();
                i++;
            }
            else if (ch is '\r' or '\n')
            {
                current.Add(field.ToString());
                field.Clear();
                if (current.Count > 1 || current[0].Length > 0)
                {
                    records.Add(current);
                }

                current = [];
                i += ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                if (records.Count > maxRows + 1)
                {
                    truncated = true;
                    break;
                }
            }
            else
            {
                field.Append(ch);
                i++;
            }

            if (field.Length > MaxFieldLength)
            {
                return Result<CsvDocument>.Invalid($"A field in line {records.Count + 1} exceeds {MaxFieldLength} characters.");
            }

            if (current.Count > MaxColumns)
            {
                return Result<CsvDocument>.Invalid($"The file has more than {MaxColumns} columns.");
            }
        }

        if (inQuotes)
        {
            return Result<CsvDocument>.Invalid("The file contains an unterminated quoted field.");
        }

        if (!truncated && (field.Length > 0 || current.Count > 0))
        {
            current.Add(field.ToString());
            if (current.Count > 1 || current[0].Length > 0)
            {
                records.Add(current);
            }
        }

        if (records.Count == 0)
        {
            return Result<CsvDocument>.Invalid("The file has no header row.");
        }

        var headers = BuildHeaders(records[0]);
        var rows = new List<string[]>();
        foreach (var record in records.Skip(1).Take(maxRows))
        {
            var row = new string[headers.Count];
            for (var c = 0; c < headers.Count; c++)
            {
                row[c] = c < record.Count ? record[c] : string.Empty;
            }

            rows.Add(row);
        }

        return Result<CsvDocument>.Success(new CsvDocument(headers, rows, separator, truncated || records.Count - 1 > maxRows));
    }

    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var header = end < 0 ? text : text[..end];
        var best = ',';
        var bestCount = 0;
        foreach (var candidate in s_delimiters)
        {
            var count = header.Count(c => c == candidate);
            if (count > bestCount)
            {
                best = candidate;
                bestCount = count;
            }
        }

        return best;
    }

    private static List<string> BuildHeaders(List<string> raw)
    {
        var headers = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var c = 0; c < raw.Count; c++)
        {
            var name = raw[c].Trim();
            if (name.Length == 0)
            {
                name = $"column{c + 1}";
            }

            var unique = name;
            for (var n = 2; !used.Add(unique); n++)
            {
                unique = $"{name}_{n}";
            }

            headers.Add(unique);
        }

        return headers;
    }
}
