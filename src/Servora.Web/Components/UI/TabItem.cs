namespace Servora.Web.Components.UI;

/// <summary>A tab definition for the <c>Tabs</c> component.</summary>
/// <param name="Key">The unique key.</param>
/// <param name="Title">The title.</param>
/// <param name="Icon">An optional icon name.</param>
/// <param name="Badge">An optional badge count.</param>
public sealed record TabItem(string Key, string Title, string? Icon = null, int? Badge = null);
