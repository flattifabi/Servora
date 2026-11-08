namespace Servora.Application.Portal;

/// <summary>The uploaded hero image of the portal home page.</summary>
/// <param name="Content">The image stream (the caller disposes it).</param>
/// <param name="ContentType">The content type detected when the image was uploaded.</param>
/// <param name="Version">The version (cache buster).</param>
public sealed record PortalHeroImage(Stream Content, string ContentType, string Version);
