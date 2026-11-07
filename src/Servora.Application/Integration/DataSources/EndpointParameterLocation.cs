namespace Servora.Application.Integration.DataSources;

/// <summary>Where an endpoint parameter is placed in the request.</summary>
public enum EndpointParameterLocation
{
    /// <summary>Replaces <c>{name}</c> in the path template (URL-encoded, dot segments rejected).</summary>
    Path = 0,

    /// <summary>Appended to the query string (URL-encoded).</summary>
    Query = 1,

    /// <summary>Only available to the JSON body template (<c>{{name}}</c>, JSON-encoded).</summary>
    Body = 2,
}
