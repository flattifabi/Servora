namespace Servora.Web.Components.Api;

/// <summary>A parameter of an API operation (from the OpenAPI document).</summary>
/// <param name="Name">The name.</param>
/// <param name="In">The location: <c>path</c>, <c>query</c> or <c>header</c>.</param>
/// <param name="Required">Whether the parameter is required.</param>
/// <param name="Type">The schema type (and format).</param>
/// <param name="Description">The description.</param>
public sealed record ApiOperationParameter(string Name, string In, bool Required, string Type, string? Description);
