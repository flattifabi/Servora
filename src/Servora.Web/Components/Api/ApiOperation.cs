namespace Servora.Web.Components.Api;

/// <summary>An operation of the REST API as listed by the API Explorer.</summary>
public sealed record ApiOperation
{
    /// <summary>Gets the HTTP method (upper case).</summary>
    public required string Method { get; init; }

    /// <summary>Gets the path template, for example <c>/api/v1/me</c>.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the tag (group).</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the operation id.</summary>
    public string? OperationId { get; init; }

    /// <summary>Gets the summary.</summary>
    public string? Summary { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the parameters.</summary>
    public IReadOnlyList<ApiOperationParameter> Parameters { get; init; } = [];

    /// <summary>Gets the request body content type, or <see langword="null"/> when the operation has no body.</summary>
    public string? RequestContentType { get; init; }

    /// <summary>Gets the request body schema as pretty JSON.</summary>
    public string? RequestSchemaJson { get; init; }

    /// <summary>Gets the documented responses.</summary>
    public IReadOnlyList<ApiOperationResponse> Responses { get; init; } = [];

    /// <summary>Gets a value indicating whether the operation allows anonymous calls.</summary>
    public bool IsAnonymous { get; init; }

    /// <summary>Gets a unique key of the operation.</summary>
    public string Key => $"{Method} {Path}";
}
