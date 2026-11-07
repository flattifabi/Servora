namespace Servora.Web.Components.Api;

/// <summary>A documented response of an API operation.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Description">The description.</param>
/// <param name="ContentType">The content type, if any.</param>
/// <param name="SchemaJson">The resolved schema as pretty JSON, if any.</param>
public sealed record ApiOperationResponse(string Status, string? Description, string? ContentType, string? SchemaJson);
