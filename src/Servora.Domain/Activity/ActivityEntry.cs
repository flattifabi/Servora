using Servora.Domain.Common;

namespace Servora.Domain.Activity;

/// <summary>
/// An entry in the activity stream (journal) of any record: comments, work notes, field changes and system events.
/// </summary>
public class ActivityEntry : Entity
{
    /// <summary>Gets or sets the technical table name of the record the entry belongs to.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the identifier of the record the entry belongs to.</summary>
    public Guid RecordId { get; set; }

    /// <summary>Gets or sets the entry type.</summary>
    public ActivityType Type { get; set; }

    /// <summary>Gets or sets the text of the entry (comment, work note or system message).</summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets a JSON array of field changes (<c>[{"field":"State","from":"New","to":"In Progress"}]</c>) for
    /// <see cref="ActivityType.FieldChange"/> entries.
    /// </summary>
    public string? ChangesJson { get; set; }

    /// <summary>Gets or sets the author user identifier, if a user created the entry.</summary>
    public Guid? AuthorId { get; set; }

    /// <summary>Gets or sets the author display name (user, API key, flow or "System").</summary>
    public string AuthorName { get; set; } = "System";

    /// <summary>Gets or sets the UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
