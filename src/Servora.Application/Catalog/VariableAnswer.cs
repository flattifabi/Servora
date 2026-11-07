using Servora.Domain.Catalog;

namespace Servora.Application.Catalog;

/// <summary>A displayable answer of a requested item's order form.</summary>
/// <param name="Name">The variable name.</param>
/// <param name="Label">The question label (the name when the variable was deleted meanwhile).</param>
/// <param name="Type">The variable type.</param>
/// <param name="Value">The display value (booleans as Yes / No, users by name).</param>
public sealed record VariableAnswer(string Name, string Label, CatalogVariableType Type, string? Value);
