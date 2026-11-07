namespace Servora.Application.Itsm;

/// <summary>Key figures shown above the problem list.</summary>
/// <param name="Open">Open problems.</param>
/// <param name="HighPriority">Open P1/P2 problems.</param>
/// <param name="Unassigned">Open problems without assignee.</param>
/// <param name="KnownErrors">Active known errors.</param>
/// <param name="LinkedOpenIncidents">Open incidents linked to an active problem.</param>
public sealed record ProblemStats(int Open, int HighPriority, int Unassigned, int KnownErrors, int LinkedOpenIncidents);
