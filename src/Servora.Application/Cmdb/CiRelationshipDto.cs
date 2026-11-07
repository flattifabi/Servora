using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>A relationship with resolved CI names.</summary>
/// <param name="Id">The relationship identifier.</param>
/// <param name="ParentId">The parent CI.</param>
/// <param name="ParentName">The parent name.</param>
/// <param name="ParentClass">The parent class label.</param>
/// <param name="ChildId">The child CI.</param>
/// <param name="ChildName">The child name.</param>
/// <param name="ChildClass">The child class label.</param>
/// <param name="Type">The type.</param>
/// <param name="Note">The note.</param>
public sealed record CiRelationshipDto(Guid Id, Guid ParentId, string ParentName, string ParentClass, Guid ChildId, string ChildName, string ChildClass, CiRelationshipType Type, string? Note);
