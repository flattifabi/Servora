using Servora.Domain.Common;

namespace Servora.Domain.Platform;

/// <summary>
/// Searchable copy of one custom field value (custom table column or custom field of a typed table). Lets conditions,
/// free-text search and sorting on JSON-stored values run as provider-neutral SQL.
/// </summary>
public class RecordFieldValue : Entity
{
    /// <summary>The maximum length of <see cref="TextValue"/>; longer values are truncated in the index only.</summary>
    public const int MaxTextLength = 400;

    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the record identifier.</summary>
    public Guid RecordId { get; set; }

    /// <summary>Gets or sets the field name.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Gets or sets the lower-case (invariant) text representation used for text comparisons.</summary>
    public string? TextValue { get; set; }

    /// <summary>Gets or sets the numeric value for number and boolean fields.</summary>
    public double? NumberValue { get; set; }

    /// <summary>Gets or sets the UTC value for date and date/time fields.</summary>
    public DateTimeOffset? DateValue { get; set; }
}
