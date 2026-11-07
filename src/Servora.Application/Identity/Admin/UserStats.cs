namespace Servora.Application.Identity.Admin;

/// <summary>KPI figures of the user list.</summary>
/// <param name="Total">All users (excluding service identities).</param>
/// <param name="Active">Active users.</param>
/// <param name="External">Users mastered by the directory or Windows.</param>
/// <param name="LockedOut">Users currently locked out.</param>
/// <param name="ActiveLast30Days">Users that signed in during the last 30 days.</param>
public sealed record UserStats(int Total, int Active, int External, int LockedOut, int ActiveLast30Days);
