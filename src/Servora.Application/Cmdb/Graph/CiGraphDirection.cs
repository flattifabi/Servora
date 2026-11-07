namespace Servora.Application.Cmdb.Graph;

/// <summary>Which side of the dependency graph is traversed from the focus CI.</summary>
public enum CiGraphDirection
{
    /// <summary>Upstream (consumers / impacted CIs) and downstream (providers / dependencies).</summary>
    Both = 0,

    /// <summary>Only CIs that depend on the focus CI (impact analysis).</summary>
    Upstream = 1,

    /// <summary>Only CIs the focus CI depends on (root cause analysis).</summary>
    Downstream = 2,
}
