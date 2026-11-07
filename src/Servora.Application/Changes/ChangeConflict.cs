using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>Another active change on the same configuration item whose planned window overlaps.</summary>
/// <param name="Id">The identifier of the conflicting change.</param>
/// <param name="Number">The number.</param>
/// <param name="ShortDescription">The short description.</param>
/// <param name="State">The state.</param>
/// <param name="PlannedStartAt">The planned start.</param>
/// <param name="PlannedEndAt">The planned end.</param>
public sealed record ChangeConflict(Guid Id, string Number, string ShortDescription, ChangeState State, DateTimeOffset? PlannedStartAt, DateTimeOffset? PlannedEndAt);
