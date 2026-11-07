namespace Servora.Application.Cmdb.Graph;

/// <summary>A bounded neighbourhood of the CMDB around a focus CI.</summary>
/// <param name="RootId">The focus CI.</param>
/// <param name="Nodes">The nodes.</param>
/// <param name="Edges">The edges between included nodes.</param>
/// <param name="Truncated">Whether nodes were omitted because the node limit was reached.</param>
public sealed record CiGraph(Guid RootId, IReadOnlyList<CiGraphNode> Nodes, IReadOnlyList<CiGraphEdge> Edges, bool Truncated);
