namespace Servora.Application.Cmdb.Graph;

/// <summary>
/// Pure, bounded breadth-first traversal of CI relationships. Downstream follows parent → child (what the focus CI depends
/// on), upstream follows child → parent (what depends on the focus CI). Levels are explored alternately so that the node limit
/// is shared fairly between both directions. Cycles are handled (every CI is visited once, at its shortest distance).
/// </summary>
public static class CiGraphBuilder
{
    /// <summary>The maximum supported depth.</summary>
    public const int MaxDepth = 6;

    /// <summary>The default node limit.</summary>
    public const int DefaultMaxNodes = 120;

    /// <summary>Traverses the relationships around a focus CI.</summary>
    /// <param name="rootId">The focus CI.</param>
    /// <param name="edges">The candidate edges (may contain edges outside the neighbourhood).</param>
    /// <param name="depth">The maximum distance (clamped to 1..<see cref="MaxDepth"/>).</param>
    /// <param name="direction">The direction(s) to follow.</param>
    /// <param name="maxNodes">The maximum number of nodes including the focus CI.</param>
    /// <returns>The traversal.</returns>
    public static CiGraphTraversal Traverse(Guid rootId, IEnumerable<CiGraphEdge> edges, int depth, CiGraphDirection direction, int maxNodes = DefaultMaxNodes)
    {
        depth = Math.Clamp(depth, 1, MaxDepth);
        maxNodes = Math.Max(1, maxNodes);
        var edgeList = edges.DistinctBy(e => e.Id).Where(e => e.ParentId != e.ChildId).ToList();
        var byParent = edgeList.ToLookup(e => e.ParentId);
        var byChild = edgeList.ToLookup(e => e.ChildId);
        var levels = new Dictionary<Guid, int> { [rootId] = 0 };
        var truncated = false;

        List<Guid> downFrontier = direction == CiGraphDirection.Upstream ? [] : [rootId];
        List<Guid> upFrontier = direction == CiGraphDirection.Downstream ? [] : [rootId];
        for (var level = 1; level <= depth; level++)
        {
            downFrontier = Expand(downFrontier, id => byParent[id].Select(e => e.ChildId), level, levels, maxNodes, ref truncated);
            upFrontier = Expand(upFrontier, id => byChild[id].Select(e => e.ParentId), -level, levels, maxNodes, ref truncated);
            if (downFrontier.Count == 0 && upFrontier.Count == 0)
            {
                break;
            }
        }

        var included = edgeList.Where(e => levels.ContainsKey(e.ParentId) && levels.ContainsKey(e.ChildId)).ToList();
        return new CiGraphTraversal(levels, included, truncated);
    }

    private static List<Guid> Expand(List<Guid> frontier, Func<Guid, IEnumerable<Guid>> neighbours, int level, Dictionary<Guid, int> levels, int maxNodes, ref bool truncated)
    {
        var next = new List<Guid>();
        foreach (var id in frontier)
        {
            foreach (var neighbour in neighbours(id))
            {
                if (levels.ContainsKey(neighbour))
                {
                    continue;
                }

                if (levels.Count >= maxNodes)
                {
                    truncated = true;
                    continue;
                }

                levels[neighbour] = level;
                next.Add(neighbour);
            }
        }

        return next;
    }
}
