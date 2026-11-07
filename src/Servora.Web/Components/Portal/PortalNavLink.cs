namespace Servora.Web.Components.Portal;

/// <summary>A link of the portal header navigation.</summary>
/// <param name="Title">The label.</param>
/// <param name="Route">The relative route.</param>
/// <param name="Icon">The icon name.</param>
public sealed record PortalNavLink(string Title, string Route, string Icon);
