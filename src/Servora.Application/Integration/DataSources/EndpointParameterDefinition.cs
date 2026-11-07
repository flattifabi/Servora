namespace Servora.Application.Integration.DataSources;

/// <summary>An allowed parameter of a data source endpoint with its validation rules (stored as JSON on the endpoint).</summary>
public sealed class EndpointParameterDefinition
{
    /// <summary>Gets or sets the name (letters, digits, underscore, dash, dot).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the location.</summary>
    public EndpointParameterLocation Location { get; set; } = EndpointParameterLocation.Query;

    /// <summary>Gets or sets the value type.</summary>
    public EndpointParameterType Type { get; set; } = EndpointParameterType.String;

    /// <summary>Gets or sets a value indicating whether the parameter is mandatory.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets an optional regular expression the whole value must match (evaluated with a timeout).</summary>
    public string? Pattern { get; set; }

    /// <summary>Gets or sets the maximum value length (1–2000).</summary>
    public int MaxLength { get; set; } = 200;

    /// <summary>Gets or sets the allowed values for <see cref="EndpointParameterType.Enum"/>.</summary>
    public List<string> AllowedValues { get; set; } = [];

    /// <summary>Gets or sets the default value used when no value is supplied.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }
}
