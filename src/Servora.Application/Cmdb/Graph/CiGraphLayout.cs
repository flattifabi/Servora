namespace Servora.Application.Cmdb.Graph;

/// <summary>
/// Pure layout algorithms for the dependency graph:
/// <list type="bullet">
/// <item><b>Layered</b> – one column per level (upstream left, focus in the middle, downstream right); nodes inside a column are
/// ordered by the barycenter of their neighbours in the adjacent column to minimise edge crossings.</item>
/// <item><b>Radial</b> – focus in the center, one ring per distance; upstream CIs on the upper half, downstream on the lower half.</item>
/// </list>
/// </summary>
public static class CiGraphLayout
{
    private const int Sweeps = 3;

    /// <summary>Computes a layered (left-to-right) layout.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="nodeWidth">The node width.</param>
    /// <param name="nodeHeight">The node height.</param>
    /// <param name="columnGap">The horizontal gap between columns.</param>
    /// <param name="rowGap">The vertical gap between nodes.</param>
    /// <param name="padding">The canvas padding.</param>
    /// <returns>The layout.</returns>
    public static CiGraphLayoutResult Layered(CiGraph graph, double nodeWidth = 210, double nodeHeight = 58, double columnGap = 96, double rowGap = 22, double padding = 36)
    {
        if (graph.Nodes.Count == 0)
        {
            return new CiGraphLayoutResult(new Dictionary<Guid, CiGraphPoint>(), padding * 2, padding * 2);
        }

        var columns = graph.Nodes.GroupBy(n => n.Level).OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.ClassLabel, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase).Select(n => n.Id).ToList());
        var neighbours = BuildNeighbours(graph);
        var minLevel = columns.Keys.Min();
        var maxLevel = columns.Keys.Max();

        for (var sweep = 0; sweep < Sweeps; sweep++)
        {
            for (var level = 1; level <= maxLevel; level++)
            {
                Reorder(columns, level, level - 1, neighbours);
            }

            for (var level = -1; level >= minLevel; level--)
            {
                Reorder(columns, level, level + 1, neighbours);
            }
        }

        var tallest = columns.Values.Max(c => c.Count);
        var contentHeight = (tallest * nodeHeight) + ((tallest - 1) * rowGap);
        var positions = new Dictionary<Guid, CiGraphPoint>();
        foreach (var (level, ids) in columns)
        {
            var x = padding + ((level - minLevel) * (nodeWidth + columnGap)) + (nodeWidth / 2);
            var columnHeight = (ids.Count * nodeHeight) + ((ids.Count - 1) * rowGap);
            var top = padding + ((contentHeight - columnHeight) / 2);
            for (var i = 0; i < ids.Count; i++)
            {
                positions[ids[i]] = new CiGraphPoint(x, top + (i * (nodeHeight + rowGap)) + (nodeHeight / 2));
            }
        }

        var width = (padding * 2) + ((maxLevel - minLevel + 1) * nodeWidth) + ((maxLevel - minLevel) * columnGap);
        return new CiGraphLayoutResult(positions, width, contentHeight + (padding * 2));
    }

    /// <summary>Computes a radial layout.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="ringGap">The minimum distance between rings.</param>
    /// <param name="minArcSpacing">The minimum arc length between two nodes on a ring.</param>
    /// <param name="padding">The canvas padding (room for labels).</param>
    /// <returns>The layout.</returns>
    public static CiGraphLayoutResult Radial(CiGraph graph, double ringGap = 150, double minArcSpacing = 120, double padding = 90)
    {
        if (graph.Nodes.Count == 0)
        {
            return new CiGraphLayoutResult(new Dictionary<Guid, CiGraphPoint>(), padding * 2, padding * 2);
        }

        var neighbours = BuildNeighbours(graph);
        var hasUp = graph.Nodes.Any(n => n.Level < 0);
        var hasDown = graph.Nodes.Any(n => n.Level > 0);
        var maxDistance = graph.Nodes.Max(n => Math.Abs(n.Level));
        var angles = new Dictionary<Guid, double> { [graph.RootId] = 0 };
        var radii = new Dictionary<int, double>();
        var groups = new List<(int Level, List<Guid> Ids, double Start, double Range)>();

        for (var distance = 1; distance <= maxDistance; distance++)
        {
            foreach (var level in new[] { -distance, distance })
            {
                var ids = graph.Nodes.Where(n => n.Level == level).Select(n => n.Id).ToList();
                if (ids.Count == 0)
                {
                    continue;
                }

                // Upstream: upper half (180°..360°), downstream: lower half (0°..180°); a lone side uses the full circle.
                var (start, range) = level < 0
                    ? hasDown ? (Math.PI * 1.08, Math.PI * 0.84) : (Math.PI * 0.5, Math.PI * 2)
                    : hasUp ? (Math.PI * 0.08, Math.PI * 0.84) : (Math.PI * 1.5, Math.PI * 2);
                var inner = level < 0 ? level + 1 : level - 1;
                ids = ids.OrderBy(id => Barycenter(id, inner, graph, neighbours, angles, start + (range / 2))).ToList();
                var radius = Math.Max(distance * ringGap, ids.Count * minArcSpacing / range);
                radii[distance] = Math.Max(radii.GetValueOrDefault(distance), radius);
                var step = range / ids.Count;
                for (var i = 0; i < ids.Count; i++)
                {
                    angles[ids[i]] = start + ((i + 0.5) * step);
                }

                groups.Add((level, ids, start, range));
            }

            // Rings never shrink outwards.
            if (distance > 1 && radii.TryGetValue(distance - 1, out var previous) && radii.TryGetValue(distance, out var current) && current < previous + (ringGap * 0.7))
            {
                radii[distance] = previous + (ringGap * 0.7);
            }
        }

        var outer = radii.Count == 0 ? 0 : radii.Values.Max();
        var size = (outer * 2) + (padding * 2);
        var center = size / 2;
        var positions = new Dictionary<Guid, CiGraphPoint> { [graph.RootId] = new CiGraphPoint(center, center) };
        foreach (var node in graph.Nodes.Where(n => n.Id != graph.RootId))
        {
            var radius = radii.GetValueOrDefault(Math.Abs(node.Level), ringGap);
            var angle = angles.GetValueOrDefault(node.Id);
            positions[node.Id] = new CiGraphPoint(center + (radius * Math.Cos(angle)), center + (radius * Math.Sin(angle)));
        }

        return new CiGraphLayoutResult(positions, size, size);
    }

    private static Dictionary<Guid, List<Guid>> BuildNeighbours(CiGraph graph)
    {
        var neighbours = graph.Nodes.ToDictionary(n => n.Id, _ => new List<Guid>());
        foreach (var edge in graph.Edges)
        {
            if (neighbours.TryGetValue(edge.ParentId, out var a) && neighbours.TryGetValue(edge.ChildId, out var b))
            {
                a.Add(edge.ChildId);
                b.Add(edge.ParentId);
            }
        }

        return neighbours;
    }

    private static void Reorder(Dictionary<int, List<Guid>> columns, int level, int referenceLevel, Dictionary<Guid, List<Guid>> neighbours)
    {
        if (!columns.TryGetValue(level, out var column) || !columns.TryGetValue(referenceLevel, out var reference))
        {
            return;
        }

        var index = reference.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => (double)t.i);
        var current = column.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => t.i);
        var ordered = column
            .Select(id =>
            {
                var positions = neighbours[id].Where(index.ContainsKey).Select(n => index[n]).ToList();
                var center = positions.Count == 0 ? current[id] * (reference.Count / (double)Math.Max(1, column.Count)) : positions.Average();
                return (id, center, original: current[id]);
            })
            .OrderBy(t => t.center)
            .ThenBy(t => t.original)
            .Select(t => t.id)
            .ToList();
        columns[level] = ordered;
    }

    private static double Barycenter(Guid id, int innerLevel, CiGraph graph, Dictionary<Guid, List<Guid>> neighbours, Dictionary<Guid, double> angles, double fallback)
    {
        var innerIds = neighbours[id].Where(n => angles.ContainsKey(n) && graph.Nodes.Any(x => x.Id == n && x.Level == innerLevel) && n != graph.RootId).ToList();
        return innerIds.Count == 0 ? fallback : innerIds.Average(n => angles[n]);
    }
}
