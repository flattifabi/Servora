namespace Servora.Application.Integration.DataSources;

/// <summary>Value type of an endpoint parameter (validated before the request is built).</summary>
public enum EndpointParameterType
{
    /// <summary>Text (optionally restricted by a pattern and maximum length).</summary>
    String = 0,

    /// <summary>Whole number.</summary>
    Integer = 1,

    /// <summary>Decimal number.</summary>
    Number = 2,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean = 3,

    /// <summary>One of the allowed values.</summary>
    Enum = 4,
}
