using Servora.Domain.Common;

namespace Servora.Domain.Integration;

/// <summary>
/// An approved operation of a <see cref="DataSource"/>: method, relative path template, allowed parameters with
/// validation rules, optional JSON body template and response mapping.
/// </summary>
public class DataSourceEndpoint : AuditableEntity
{
    /// <summary>Gets or sets the owning data source.</summary>
    public Guid DataSourceId { get; set; }

    /// <summary>Gets or sets the key used for execution, unique per data source (lower case, digits, dots, dashes, underscores).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the HTTP method (GET, POST, PUT, PATCH, DELETE).</summary>
    public string Method { get; set; } = "GET";

    /// <summary>Gets or sets the path template relative to the base URL, for example <c>/users/{id}</c>.</summary>
    public string PathTemplate { get; set; } = "/";

    /// <summary>Gets or sets the JSON array of parameter definitions.</summary>
    public string ParametersJson { get; set; } = "[]";

    /// <summary>Gets or sets the JSON body template with <c>{{parameter}}</c> placeholders (values are JSON-encoded).</summary>
    public string? BodyTemplate { get; set; }

    /// <summary>Gets or sets the JSON path of the relevant response part, for example <c>data.items</c>.</summary>
    public string? ResponsePath { get; set; }

    /// <summary>Gets or sets a value indicating whether the endpoint can be executed.</summary>
    public bool IsEnabled { get; set; } = true;
}
