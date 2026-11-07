namespace Servora.Application.Sla;

/// <summary>A task SLA timer row for monitoring lists.</summary>
/// <param name="Id">The timer identifier.</param>
/// <param name="DefinitionName">The SLA definition name.</param>
/// <param name="TableName">The table.</param>
/// <param name="RecordId">The record.</param>
/// <param name="RecordDisplay">The record display value.</param>
/// <param name="Stage">The display stage.</param>
/// <param name="StartedAt">The UTC start time.</param>
/// <param name="DueAt">The UTC due time.</param>
/// <param name="PercentElapsed">The elapsed percentage.</param>
/// <param name="Breached">Whether the timer breached.</param>
/// <param name="Url">The record route.</param>
public sealed record SlaTimerRow(Guid Id, string DefinitionName, string TableName, Guid RecordId, string? RecordDisplay, string Stage, DateTimeOffset StartedAt, DateTimeOffset? DueAt, double PercentElapsed, bool Breached, string Url);
