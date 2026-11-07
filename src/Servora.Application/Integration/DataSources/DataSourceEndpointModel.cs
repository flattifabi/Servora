namespace Servora.Application.Integration.DataSources;

/// <summary>Editable definition of a data source endpoint.</summary>
public sealed class DataSourceEndpointModel
{
    /// <summary>Gets or sets the identifier (empty for new endpoints).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the data source.</summary>
    public Guid DataSourceId { get; set; }

    /// <summary>Gets or sets the key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the HTTP method.</summary>
    public string Method { get; set; } = "GET";

    /// <summary>Gets or sets the path template.</summary>
    public string PathTemplate { get; set; } = "/";

    /// <summary>Gets or sets the parameters.</summary>
    public List<EndpointParameterDefinition> Parameters { get; set; } = [];

    /// <summary>Gets or sets the JSON body template.</summary>
    public string? BodyTemplate { get; set; }

    /// <summary>Gets or sets the response JSON path.</summary>
    public string? ResponsePath { get; set; }

    /// <summary>Gets or sets a value indicating whether the endpoint is enabled.</summary>
    public bool IsEnabled { get; set; } = true;
}
