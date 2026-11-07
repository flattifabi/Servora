namespace Servora.Web.Navigation;

/// <summary>A navigation target shown in the sidebar and the command palette.</summary>
/// <param name="Title">The display title.</param>
/// <param name="Route">The relative route.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="Permission">The permission required to see the entry; <see langword="null"/> means any authenticated user.</param>
/// <param name="Keywords">Additional search keywords for the command palette.</param>
public sealed record NavEntry(string Title, string Route, string Icon, string? Permission = null, string? Keywords = null);
