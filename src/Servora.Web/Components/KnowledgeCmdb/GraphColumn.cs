namespace Servora.Web.Components.KnowledgeCmdb;

/// <summary>A column header of the layered dependency graph.</summary>
/// <param name="Level">The level.</param>
/// <param name="X">The center x coordinate.</param>
/// <param name="Label">The label.</param>
public sealed record GraphColumn(int Level, double X, string Label);
