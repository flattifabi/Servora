namespace Servora.Application.Identity.Admin;

/// <summary>KPI figures of the group list.</summary>
/// <param name="Total">All groups.</param>
/// <param name="Assignment">Assignment groups.</param>
/// <param name="Approval">Approval groups.</param>
/// <param name="Security">Security groups.</param>
/// <param name="Memberships">Total memberships.</param>
/// <param name="Empty">Active groups without members.</param>
public sealed record GroupStats(int Total, int Assignment, int Approval, int Security, int Memberships, int Empty);
