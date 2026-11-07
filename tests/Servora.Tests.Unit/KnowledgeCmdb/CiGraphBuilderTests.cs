using Servora.Application.Cmdb.Graph;
using Servora.Domain.Cmdb;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Tests for the dependency graph traversal and layouts.</summary>
public sealed class CiGraphBuilderTests
{
    // Service → App → Server → Host, plus Db used by App, and a second service using the same App.
    private static readonly Guid Service = Guid.NewGuid();
    private static readonly Guid Service2 = Guid.NewGuid();
    private static readonly Guid App = Guid.NewGuid();
    private static readonly Guid Server = Guid.NewGuid();
    private static readonly Guid Host = Guid.NewGuid();
    private static readonly Guid Db = Guid.NewGuid();
    private static readonly Guid Unrelated = Guid.NewGuid();

    private static readonly CiGraphEdge[] s_edges =
    [
        Edge(Service, App, CiRelationshipType.DependsOn),
        Edge(Service2, App, CiRelationshipType.DependsOn),
        Edge(App, Server, CiRelationshipType.RunsOn),
        Edge(App, Db, CiRelationshipType.DependsOn),
        Edge(Server, Host, CiRelationshipType.HostedOn),
        Edge(Unrelated, Guid.NewGuid(), CiRelationshipType.ConnectsTo),
    ];

    [Fact]
    public void Both_AssignsNegativeLevelsUpstreamAndPositiveDownstream()
    {
        var result = CiGraphBuilder.Traverse(App, s_edges, 2, CiGraphDirection.Both);

        Assert.Equal(0, result.Levels[App]);
        Assert.Equal(-1, result.Levels[Service]);
        Assert.Equal(-1, result.Levels[Service2]);
        Assert.Equal(1, result.Levels[Server]);
        Assert.Equal(1, result.Levels[Db]);
        Assert.Equal(2, result.Levels[Host]);
        Assert.False(result.Levels.ContainsKey(Unrelated));
        Assert.Equal(5, result.Edges.Count);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void Depth_LimitsTheTraversal()
    {
        var result = CiGraphBuilder.Traverse(Service, s_edges, 1, CiGraphDirection.Both);

        Assert.Equal([Service, App], result.Levels.Keys.OrderBy(k => k == Service ? 0 : 1));
        Assert.Single(result.Edges);
    }

    [Fact]
    public void Downstream_IgnoresConsumers()
    {
        var result = CiGraphBuilder.Traverse(App, s_edges, 3, CiGraphDirection.Downstream);

        Assert.DoesNotContain(Service, result.Levels.Keys);
        Assert.Contains(Host, result.Levels.Keys);
    }

    [Fact]
    public void Upstream_ShowsImpactedServicesOnly()
    {
        var result = CiGraphBuilder.Traverse(Host, s_edges, 4, CiGraphDirection.Upstream);

        Assert.Equal(-1, result.Levels[Server]);
        Assert.Equal(-2, result.Levels[App]);
        Assert.Equal(-3, result.Levels[Service]);
        Assert.Equal(-3, result.Levels[Service2]);
        Assert.DoesNotContain(Db, result.Levels.Keys);
    }

    [Fact]
    public void Cycles_AreVisitedOnce()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var edges = new[] { Edge(a, b, CiRelationshipType.ConnectsTo), Edge(b, c, CiRelationshipType.ConnectsTo), Edge(c, a, CiRelationshipType.ConnectsTo), Edge(a, a, CiRelationshipType.ConnectsTo) };

        var result = CiGraphBuilder.Traverse(a, edges, 6, CiGraphDirection.Both);

        Assert.Equal(3, result.Levels.Count);
        Assert.Equal(3, result.Edges.Count);
    }

    [Fact]
    public void MaxNodes_TruncatesAndFlags()
    {
        var root = Guid.NewGuid();
        var edges = Enumerable.Range(0, 50).Select(_ => Edge(root, Guid.NewGuid(), CiRelationshipType.DependsOn)).ToList();

        var result = CiGraphBuilder.Traverse(root, edges, 2, CiGraphDirection.Both, maxNodes: 10);

        Assert.Equal(10, result.Levels.Count);
        Assert.True(result.Truncated);
        Assert.Equal(9, result.Edges.Count);
    }

    [Fact]
    public void LayeredLayout_PlacesUpstreamLeftOfFocusAndDownstreamRight()
    {
        var graph = BuildGraph(App, CiGraphBuilder.Traverse(App, s_edges, 2, CiGraphDirection.Both));

        var layout = CiGraphLayout.Layered(graph);

        Assert.True(layout.Positions[Service].X < layout.Positions[App].X);
        Assert.True(layout.Positions[App].X < layout.Positions[Server].X);
        Assert.True(layout.Positions[Server].X < layout.Positions[Host].X);
        Assert.Equal(layout.Positions[Server].X, layout.Positions[Db].X);
        Assert.NotEqual(layout.Positions[Server].Y, layout.Positions[Db].Y);
        Assert.All(layout.Positions.Values, p => Assert.InRange(p.X, 0, layout.Width));
        Assert.All(layout.Positions.Values, p => Assert.InRange(p.Y, 0, layout.Height));
    }

    [Fact]
    public void RadialLayout_CentersFocusAndSeparatesSides()
    {
        var graph = BuildGraph(App, CiGraphBuilder.Traverse(App, s_edges, 2, CiGraphDirection.Both));

        var layout = CiGraphLayout.Radial(graph);
        var center = layout.Positions[App];

        Assert.Equal(layout.Width / 2, center.X, 3);
        Assert.True(layout.Positions[Service].Y < center.Y, "Upstream nodes are placed above the focus.");
        Assert.True(layout.Positions[Server].Y > center.Y, "Downstream nodes are placed below the focus.");
        var distance1 = Distance(center, layout.Positions[Server]);
        var distance2 = Distance(center, layout.Positions[Host]);
        Assert.True(distance2 > distance1);
    }

    private static double Distance(CiGraphPoint a, CiGraphPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static CiGraph BuildGraph(Guid root, CiGraphTraversal traversal) =>
        new(root, traversal.Levels.Select(kv => new CiGraphNode(kv.Key, kv.Key.ToString()[..8], "server", "Server", "server", "#000000", OperationalStatus.Operational, kv.Value)).ToList(), traversal.Edges, traversal.Truncated);

    private static CiGraphEdge Edge(Guid parent, Guid child, CiRelationshipType type) => new(Guid.NewGuid(), parent, child, type);
}
