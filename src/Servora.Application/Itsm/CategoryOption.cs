namespace Servora.Application.Itsm;

/// <summary>A selectable incident category with its subcategories.</summary>
/// <param name="Name">The category name.</param>
/// <param name="Subcategories">The subcategory names in display order.</param>
public sealed record CategoryOption(string Name, IReadOnlyList<string> Subcategories);
