namespace Servora.Application.Changes;

/// <summary>Key figures of change management.</summary>
/// <param name="Open">Active changes.</param>
/// <param name="AwaitingApproval">Changes waiting for CAB authorization.</param>
/// <param name="ScheduledNext7Days">Scheduled changes starting within the next 7 days.</param>
/// <param name="InImplementation">Changes currently being implemented.</param>
/// <param name="OpenEmergency">Active emergency changes.</param>
/// <param name="SuccessRate">Share of successfully closed changes of the last 90 days in percent, or <see langword="null"/> without closed changes.</param>
public sealed record ChangeStats(int Open, int AwaitingApproval, int ScheduledNext7Days, int InImplementation, int OpenEmergency, double? SuccessRate);
