using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>KPIs of the CMDB.</summary>
/// <param name="Total">The total number of CIs.</param>
/// <param name="ByStatus">The number of CIs per operational status.</param>
/// <param name="ByClass">The number of CIs per class (classes without CIs included).</param>
/// <param name="RelationshipCount">The number of relationships.</param>
/// <param name="MissionCritical">The number of CIs with criticality 1.</param>
public sealed record CmdbOverview(
    int Total,
    IReadOnlyDictionary<OperationalStatus, int> ByStatus,
    IReadOnlyList<CiClassCount> ByClass,
    int RelationshipCount,
    int MissionCritical);
