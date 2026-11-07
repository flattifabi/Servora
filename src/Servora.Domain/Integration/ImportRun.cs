using Servora.Domain.Common;

namespace Servora.Domain.Integration;

/// <summary>One execution of an <see cref="ImportSet"/> with its staged rows (<see cref="ImportRow"/>) and counters.</summary>
public class ImportRun : Entity
{
    /// <summary>Gets or sets the import set.</summary>
    public Guid ImportSetId { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public ImportRunStatus Status { get; set; } = ImportRunStatus.Running;

    /// <summary>Gets or sets a description of the source (file name or endpoint).</summary>
    public string? SourceDescription { get; set; }

    /// <summary>Gets or sets the UTC start time.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets the UTC completion time.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the number of staged rows.</summary>
    public int TotalRows { get; set; }

    /// <summary>Gets or sets the number of inserted records.</summary>
    public int Inserted { get; set; }

    /// <summary>Gets or sets the number of updated records.</summary>
    public int Updated { get; set; }

    /// <summary>Gets or sets the number of skipped rows.</summary>
    public int Skipped { get; set; }

    /// <summary>Gets or sets the number of failed rows.</summary>
    public int Errors { get; set; }

    /// <summary>Gets or sets the display name of the actor that started the run.</summary>
    public string? StartedBy { get; set; }

    /// <summary>Gets or sets the user-safe error message for failed runs.</summary>
    public string? Error { get; set; }
}
