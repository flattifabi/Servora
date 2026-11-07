namespace Servora.Application.Cmdb.Graph;

/// <summary>Result of <see cref="CiGraphBuilder.Traverse"/>.</summary>
/// <param name="Levels">The included CIs and their level (negative = upstream, positive = downstream).</param>
/// <param name="Edges">The edges whose both ends are included.</param>
/// <param name="Truncated">Whether the node limit cut the traversal.</param>
public sealed record CiGraphTraversal(IReadOnlyDictionary<Guid, int> Levels, IReadOnlyList<CiGraphEdge> Edges, bool Truncated);
