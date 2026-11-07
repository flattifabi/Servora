using Servora.Domain.Changes;

namespace Servora.Application.Changes;

/// <summary>A change shown on the change calendar.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Number">The number.</param>
/// <param name="ShortDescription">The short description.</param>
/// <param name="Type">The type.</param>
/// <param name="State">The state.</param>
/// <param name="Risk">The risk.</param>
/// <param name="PlannedStartAt">The planned start.</param>
/// <param name="PlannedEndAt">The planned end.</param>
/// <param name="ConfigurationItemName">The configuration item name.</param>
public sealed record ChangeCalendarEntry(
    Guid Id,
    string Number,
    string ShortDescription,
    ChangeType Type,
    ChangeState State,
    ChangeRisk Risk,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset PlannedEndAt,
    string? ConfigurationItemName);
