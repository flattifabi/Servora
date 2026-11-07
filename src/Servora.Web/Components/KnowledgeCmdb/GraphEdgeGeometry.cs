using Servora.Application.Cmdb.Graph;

namespace Servora.Web.Components.KnowledgeCmdb;

/// <summary>SVG path and label anchor of a rendered graph edge.</summary>
/// <param name="Path">The SVG path data.</param>
/// <param name="Mid">The label anchor point.</param>
public sealed record GraphEdgeGeometry(string Path, CiGraphPoint Mid);
