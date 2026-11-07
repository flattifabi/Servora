using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Domain.Changes;

/// <summary>A unit of work that is part of implementing a <see cref="ChangeRequest"/>.</summary>
[RecordTable(ChangeTables.ChangeTask, "Change Task", PluralLabel = "Change Tasks", Icon = "clipboard-list", Route = "/changes")]
public class ChangeTask : TaskRecord
{
    /// <summary>Gets or sets the parent change request.</summary>
    [Reference(TableNames.ChangeRequest)]
    public Guid ChangeRequestId { get; set; }

    /// <summary>Gets or sets the task state.</summary>
    public ChangeTaskState State { get; set; } = ChangeTaskState.Open;

    /// <summary>Gets or sets the UTC planned start.</summary>
    public DateTimeOffset? PlannedStartAt { get; set; }

    /// <summary>Gets or sets the UTC planned end.</summary>
    public DateTimeOffset? PlannedEndAt { get; set; }

    /// <summary>Gets or sets the closure notes.</summary>
    public string? CloseNotes { get; set; }

    /// <summary>Gets or sets the sort order within the change.</summary>
    public int Order { get; set; }
}
