using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>A new relationship: "parent &lt;type&gt; child".</summary>
/// <param name="ParentId">The parent (upstream) CI.</param>
/// <param name="ChildId">The child (downstream) CI.</param>
/// <param name="Type">The type.</param>
/// <param name="Note">An optional note.</param>
public sealed record CiRelationshipInput(Guid ParentId, Guid ChildId, CiRelationshipType Type, string? Note = null);
