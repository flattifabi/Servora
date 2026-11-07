namespace Servora.Application.Sla;

/// <summary>SLA monitoring figures for the studio.</summary>
/// <param name="Running">The number of running or paused timers.</param>
/// <param name="RunningBreached">The number of running timers past their target.</param>
/// <param name="CompletedLast30Days">The number of timers completed in the last 30 days.</param>
/// <param name="AchievedLast30Days">The number of those completed within target.</param>
/// <param name="Timers">The most urgent running timers.</param>
public sealed record SlaOverview(int Running, int RunningBreached, int CompletedLast30Days, int AchievedLast30Days, IReadOnlyList<SlaTimerRow> Timers)
{
    /// <summary>Gets the achievement rate of the last 30 days in percent (100 when nothing completed).</summary>
    public double ComplianceRate => CompletedLast30Days == 0 ? 100 : Math.Round(AchievedLast30Days * 100d / CompletedLast30Days, 1);
}
