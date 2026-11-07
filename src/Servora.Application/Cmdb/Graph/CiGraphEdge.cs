using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb.Graph;

/// <summary>A directed edge of the dependency graph: "parent &lt;type&gt; child".</summary>
/// <param name="Id">The relationship identifier.</param>
/// <param name="ParentId">The parent (upstream) CI.</param>
/// <param name="ChildId">The child (downstream) CI.</param>
/// <param name="Type">The relationship type.</param>
public sealed record CiGraphEdge(Guid Id, Guid ParentId, Guid ChildId, CiRelationshipType Type);
