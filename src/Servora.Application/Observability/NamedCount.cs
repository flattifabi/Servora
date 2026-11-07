namespace Servora.Application.Observability;

/// <summary>A label with a count (facets, breakdowns).</summary>
/// <param name="Name">The label.</param>
/// <param name="Count">The count.</param>
public sealed record NamedCount(string Name, int Count);
