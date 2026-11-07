namespace Servora.Application.Configuration;

/// <summary>The stored logo image, opened for download.</summary>
/// <param name="Content">The image stream; the caller disposes it.</param>
/// <param name="ContentType">The verified image content type.</param>
/// <param name="Version">The version stamp (used as ETag).</param>
public sealed record BrandingLogo(Stream Content, string ContentType, string Version);
