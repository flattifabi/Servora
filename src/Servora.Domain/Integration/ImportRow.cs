using Servora.Domain.Common;

namespace Servora.Domain.Integration;

/// <summary>A staged source row of an <see cref="ImportRun"/> and the result of transforming it into the target table.</summary>
public class ImportRow : Entity
{
    /// <summary>Gets or sets the run.</summary>
    public Guid RunId { get; set; }

    /// <summary>Gets or sets the 1-based row number in the source.</summary>
    public int RowNumber { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public ImportRowStatus Status { get; set; } = ImportRowStatus.Pending;

    /// <summary>Gets or sets the staged source data as JSON object (truncated to a safe size).</summary>
    public string DataJson { get; set; } = "{}";

    /// <summary>Gets or sets the created or updated target record.</summary>
    public Guid? TargetRecordId { get; set; }

    /// <summary>Gets or sets the result message.</summary>
    public string? Message { get; set; }
}
