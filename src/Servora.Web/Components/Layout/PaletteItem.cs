namespace Servora.Web.Components.Layout;

/// <summary>An entry of the command palette.</summary>
/// <param name="Group">The result group header.</param>
/// <param name="Title">The title.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="Hint">The hint shown on the right.</param>
/// <param name="SearchText">The text matched against the search term.</param>
/// <param name="Target">The navigation target or action key.</param>
public sealed record PaletteItem(string Group, string Title, string Icon, string Hint, string SearchText, string Target);
