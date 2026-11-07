namespace Servora.Application.Automation.Services;

/// <summary>Key figures of the automation area.</summary>
/// <param name="TotalFlows">The number of flows.</param>
/// <param name="ActiveFlows">The number of active flows.</param>
/// <param name="Runs24h">Runs started in the last 24 hours.</param>
/// <param name="Failed24h">Runs failed in the last 24 hours.</param>
/// <param name="Waiting">Runs currently waiting.</param>
/// <param name="Running">Runs currently queued or running.</param>
/// <param name="SuccessRate">The success rate of completed runs in the last 24 hours (0–100).</param>
/// <param name="AverageDurationMs">The average duration of completed runs in the last 24 hours.</param>
/// <param name="HourlyRuns">Runs per hour for the last 24 hours (oldest first).</param>
public sealed record FlowOverview(
    int TotalFlows,
    int ActiveFlows,
    int Runs24h,
    int Failed24h,
    int Waiting,
    int Running,
    double SuccessRate,
    long AverageDurationMs,
    IReadOnlyList<double> HourlyRuns);
