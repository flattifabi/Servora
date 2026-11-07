namespace Servora.Web.Navigation;

/// <summary>A titled group of navigation entries.</summary>
/// <param name="Title">The section title.</param>
/// <param name="Entries">The entries.</param>
public sealed record NavSection(string Title, IReadOnlyList<NavEntry> Entries);
