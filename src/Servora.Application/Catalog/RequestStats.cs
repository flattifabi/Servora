namespace Servora.Application.Catalog;

/// <summary>Key figures of request fulfilment.</summary>
/// <param name="OpenRequests">Active requests.</param>
/// <param name="AwaitingApproval">Requested items waiting for approval.</param>
/// <param name="OpenTasks">Open catalog tasks.</param>
/// <param name="UnassignedTasks">Open catalog tasks without assignee.</param>
/// <param name="CompletedLast30Days">Requests completed in the last 30 days.</param>
public sealed record RequestStats(int OpenRequests, int AwaitingApproval, int OpenTasks, int UnassignedTasks, int CompletedLast30Days);
