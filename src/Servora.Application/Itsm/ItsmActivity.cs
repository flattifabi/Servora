using System.Text.Json;
using Servora.Application.Common;
using Servora.Domain.Activity;

namespace Servora.Application.Itsm;

/// <summary>
/// Builds activity stream entries for incidents and problems. The entries are added to the same unit of work as the
/// record change, so the journal and the record are always committed together.
/// </summary>
internal static class ItsmActivity
{
    private const int MaxTextLength = 20_000;

    /// <summary>Creates a system entry (state transition, link, assignment).</summary>
    /// <param name="table">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="text">The message.</param>
    /// <param name="actor">The acting user.</param>
    /// <param name="now">The UTC time.</param>
    /// <returns>The entry.</returns>
    public static ActivityEntry System(string table, Guid recordId, string text, ICurrentUser actor, DateTimeOffset now) =>
        Create(table, recordId, ActivityType.System, text, actor, now);

    /// <summary>Creates an internal work note.</summary>
    /// <param name="table">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="text">The note.</param>
    /// <param name="actor">The acting user.</param>
    /// <param name="now">The UTC time.</param>
    /// <returns>The entry.</returns>
    public static ActivityEntry WorkNote(string table, Guid recordId, string text, ICurrentUser actor, DateTimeOffset now) =>
        Create(table, recordId, ActivityType.WorkNote, text, actor, now);

    /// <summary>Creates a field change entry, or returns <see langword="null"/> when nothing changed.</summary>
    /// <param name="table">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="changes">The changed fields with display values.</param>
    /// <param name="actor">The acting user.</param>
    /// <param name="now">The UTC time.</param>
    /// <returns>The entry or <see langword="null"/>.</returns>
    public static ActivityEntry? FieldChanges(string table, Guid recordId, IReadOnlyList<(string Field, string? From, string? To)> changes, ICurrentUser actor, DateTimeOffset now)
    {
        if (changes.Count == 0)
        {
            return null;
        }

        var entry = Create(table, recordId, ActivityType.FieldChange, null, actor, now);
        entry.ChangesJson = JsonSerializer.Serialize(changes.Select(c => new
        {
            field = c.Field,
            from = Shorten(c.From) ?? "—",
            to = Shorten(c.To) ?? "—",
        }));
        return entry;
    }

    /// <summary>Adds a change to the list when the display values differ.</summary>
    /// <param name="changes">The change list.</param>
    /// <param name="field">The field label.</param>
    /// <param name="from">The previous display value.</param>
    /// <param name="to">The new display value.</param>
    public static void Track(List<(string Field, string? From, string? To)> changes, string field, string? from, string? to)
    {
        from = string.IsNullOrWhiteSpace(from) ? null : from;
        to = string.IsNullOrWhiteSpace(to) ? null : to;
        if (!string.Equals(from, to, StringComparison.Ordinal))
        {
            changes.Add((field, from, to));
        }
    }

    private static ActivityEntry Create(string table, Guid recordId, ActivityType type, string? text, ICurrentUser actor, DateTimeOffset now) => new()
    {
        TableName = table,
        RecordId = recordId,
        Type = type,
        Text = text is { Length: > MaxTextLength } ? text[..MaxTextLength] : text,
        AuthorId = actor.UserId,
        AuthorName = actor.DisplayName,
        CreatedAt = now,
    };

    private static string? Shorten(string? value) => value is { Length: > 140 } ? value[..137] + "…" : value;
}
