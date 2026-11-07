namespace Servora.Application.Itsm;

/// <summary>Key figures shown above the incident list.</summary>
/// <param name="Open">Open incidents (new, in progress, on hold).</param>
/// <param name="Critical">Open P1 incidents.</param>
/// <param name="Unassigned">Open incidents without assignee.</param>
/// <param name="SlaBreached">Active incidents with at least one breached SLA (sampled, see remarks of the service).</param>
/// <param name="MajorOpen">Open major incidents.</param>
/// <param name="ResolvedLast24Hours">Incidents resolved during the last 24 hours.</param>
/// <param name="OpenedTrend">Incidents opened per day over the last 7 days (oldest first).</param>
/// <param name="ResolvedTrend">Incidents resolved per day over the last 7 days (oldest first).</param>
public sealed record IncidentStats(
    int Open,
    int Critical,
    int Unassigned,
    int SlaBreached,
    int MajorOpen,
    int ResolvedLast24Hours,
    IReadOnlyList<double> OpenedTrend,
    IReadOnlyList<double> ResolvedTrend);
