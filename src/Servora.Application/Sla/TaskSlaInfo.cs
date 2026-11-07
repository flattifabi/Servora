namespace Servora.Application.Sla;

/// <summary>SLA timer attached to a task record, for display on forms and lists.</summary>
/// <param name="Name">The SLA definition name.</param>
/// <param name="Stage">The stage: InProgress, Paused, Achieved, Breached or Cancelled.</param>
/// <param name="StartedAt">The UTC start time.</param>
/// <param name="DueAt">The UTC breach time.</param>
/// <param name="PercentElapsed">Elapsed business time in percent of the target (can exceed 100).</param>
/// <param name="Breached">Whether the SLA is breached.</param>
public sealed record TaskSlaInfo(string Name, string Stage, DateTimeOffset StartedAt, DateTimeOffset? DueAt, double PercentElapsed, bool Breached);
