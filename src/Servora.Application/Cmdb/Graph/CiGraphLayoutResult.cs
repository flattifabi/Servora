namespace Servora.Application.Cmdb.Graph;

/// <summary>Node positions and canvas size of a laid out graph.</summary>
/// <param name="Positions">The center point per CI.</param>
/// <param name="Width">The canvas width.</param>
/// <param name="Height">The canvas height.</param>
public sealed record CiGraphLayoutResult(IReadOnlyDictionary<Guid, CiGraphPoint> Positions, double Width, double Height);
