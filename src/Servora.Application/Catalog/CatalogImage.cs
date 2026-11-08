namespace Servora.Application.Catalog;

/// <summary>An opened catalog item image.</summary>
/// <param name="Content">The image stream (the caller disposes it).</param>
/// <param name="ContentType">The content type detected from the file content.</param>
public sealed record CatalogImage(Stream Content, string ContentType);
