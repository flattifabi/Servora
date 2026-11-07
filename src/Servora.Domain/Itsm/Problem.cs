using Servora.Domain.Common;

namespace Servora.Domain.Itsm;

/// <summary>
/// The underlying cause of one or more incidents. Problem management finds root causes, documents workarounds as
/// known errors and drives permanent fixes. State changes are validated by <see cref="ProblemStateMachine"/>.
/// </summary>
[RecordTable(TableNames.Problem, "Problem", PluralLabel = "Problems", Icon = "lightbulb", Route = "/problems")]
public class Problem : TaskRecord
{
    /// <summary>The number prefix of problems.</summary>
    public const string NumberPrefix = "PRB";

    /// <summary>Gets or sets the lifecycle state.</summary>
    public ProblemState State { get; set; } = ProblemState.New;

    /// <summary>Gets or sets the category name (see <see cref="IncidentCategory"/>).</summary>
    public string? Category { get; set; }

    /// <summary>Gets or sets the affected business service.</summary>
    [Reference(TableNames.ConfigurationItem)]
    public Guid? BusinessServiceId { get; set; }

    /// <summary>Gets or sets a value indicating whether the problem is a documented known error.</summary>
    public bool IsKnownError { get; set; }

    /// <summary>Gets or sets the workaround that restores service until a fix is available.</summary>
    public string? Workaround { get; set; }

    /// <summary>Gets or sets the identified root cause.</summary>
    public string? RootCause { get; set; }

    /// <summary>Gets or sets the description of the permanent fix (mandatory when resolved).</summary>
    public string? FixNotes { get; set; }

    /// <summary>Gets or sets the resolution code (mandatory when resolved).</summary>
    public ProblemResolutionCode? ResolutionCode { get; set; }

    /// <summary>Gets or sets the UTC time the problem was resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Gets or sets the user that resolved the problem.</summary>
    [Reference(TableNames.User)]
    public Guid? ResolvedById { get; set; }
}
