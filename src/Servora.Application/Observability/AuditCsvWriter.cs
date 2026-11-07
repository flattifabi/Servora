using System.Globalization;
using System.Text;
using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>
/// Writes audit events as RFC 4180 CSV. Values starting with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or carriage return
/// are prefixed with an apostrophe to prevent formula injection when the file is opened in a spreadsheet.
/// </summary>
public static class AuditCsvWriter
{
    /// <summary>The header line.</summary>
    public const string Header = "Sequence,OccurredAtUtc,Category,Action,Outcome,ActorType,ActorName,ActorId,TargetType,TargetId,TargetName,Message,IpAddress,CorrelationId,Details,Hash,PreviousHash";

    /// <summary>Appends the header line.</summary>
    /// <param name="builder">The target.</param>
    public static void WriteHeader(StringBuilder builder) => builder.Append(Header).Append("\r\n");

    /// <summary>Appends one event as a CSV line.</summary>
    /// <param name="builder">The target.</param>
    /// <param name="auditEvent">The event.</param>
    public static void WriteRow(StringBuilder builder, AuditEvent auditEvent)
    {
        string?[] values =
        [
            auditEvent.Sequence.ToString(CultureInfo.InvariantCulture),
            auditEvent.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            auditEvent.Category,
            auditEvent.Action,
            auditEvent.Outcome.ToString(),
            auditEvent.ActorType,
            auditEvent.ActorName,
            auditEvent.ActorId,
            auditEvent.TargetType,
            auditEvent.TargetId,
            auditEvent.TargetName,
            auditEvent.Message,
            auditEvent.IpAddress,
            auditEvent.CorrelationId,
            auditEvent.DetailsJson,
            auditEvent.Hash,
            auditEvent.PreviousHash,
        ];

        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(Escape(values[i]));
        }

        builder.Append("\r\n");
    }

    /// <summary>Escapes one CSV value (quoting and formula-injection protection).</summary>
    /// <param name="value">The value.</param>
    /// <returns>The escaped value.</returns>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }
}
