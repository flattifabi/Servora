using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb.Graph;

/// <summary>A node of the dependency graph.</summary>
/// <param name="Id">The CI identifier.</param>
/// <param name="Name">The CI name.</param>
/// <param name="ClassKey">The class key.</param>
/// <param name="ClassLabel">The class label.</param>
/// <param name="Icon">The class icon.</param>
/// <param name="Color">The class color.</param>
/// <param name="Status">The operational status.</param>
/// <param name="Level">The distance from the focus CI: negative = upstream, 0 = focus, positive = downstream.</param>
public sealed record CiGraphNode(Guid Id, string Name, string ClassKey, string ClassLabel, string Icon, string Color, OperationalStatus Status, int Level);
