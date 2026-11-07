using Servora.Domain.Common;

namespace Servora.Domain.Platform;

/// <summary>Form layout (sections with ordered fields) and default list columns of a table, edited in the studio.</summary>
public class TableLayout : AuditableEntity
{
    /// <summary>Gets or sets the technical table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the form sections as JSON array of <c>{ "title", "fields": [] }</c>.</summary>
    public string? FormJson { get; set; }

    /// <summary>Gets or sets the default list columns as JSON array of field names.</summary>
    public string? ListColumnsJson { get; set; }
}
