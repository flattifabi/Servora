namespace Servora.Application.Portal;

/// <summary>A labelled fact shown in portal side panels.</summary>
/// <param name="Label">The label.</param>
/// <param name="Value">The display value.</param>
public sealed record PortalFact(string Label, string? Value);
